using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Notifications;
using Darts.GameSdk;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Darts.Infrastructure.External.Detection;

/// <summary>
/// The sole consumer of the active IDetectionSource's event stream. GameCommandExecutor is scoped
/// (it depends on IMatchRepository/IUnitOfWork), so a fresh scope is opened per event — throws are
/// human-paced, so per-event scoping is simple and cheap enough.
/// </summary>
public sealed class DetectionListenerService(
    IDetectionSource detectionSource,
    IServiceScopeFactory scopeFactory,
    ILogger<DetectionListenerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in detectionSource.EventsAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<GameCommandExecutor>();

            var result = evt.Type switch
            {
                DetectionEventType.Throw => await executor.RecordThrow(evt.Throw!.BoardId, evt.Throw, stoppingToken),
                DetectionEventType.EndOfTurn => await executor.RecordEndOfTurn(evt.BoardId, stoppingToken),
                _ => throw new ArgumentOutOfRangeException(nameof(evt), evt.Type, "Unknown detection event type."),
            };

            if (result.IsError)
            {
                logger.LogWarning(
                    "Dropped {EventType} for board '{BoardId}': {Errors}",
                    evt.Type,
                    evt.BoardId,
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
