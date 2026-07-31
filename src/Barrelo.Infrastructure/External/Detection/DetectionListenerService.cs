using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using ErrorOr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>
/// The sole consumer of the active IDetectionSource's event stream. GameCommandExecutor is scoped
/// (it depends on the scoped IDispatcher), so a fresh scope is opened per event — throws are
/// human-paced, so per-event scoping is simple and cheap enough.
/// </summary>
public sealed class DetectionListenerService(
    IDetectionSource detectionSource,
    IServiceScopeFactory scopeFactory,
    ILogger<DetectionListenerService> logger)
    : BackgroundService
{
    // Detectors that report a whole visit at a time are reduced to per-dart events here, in one place, so
    // IGame never needs a second way to be told about a throw.
    private readonly VisitDiffer _visitDiffer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var evt in detectionSource.EventsAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var executor = scope.ServiceProvider.GetRequiredService<GameCommandExecutor>();

            switch (evt.Type)
            {
                case DetectionEventType.Throw:
                    await Run(executor.RecordThrow(evt.Throw!, stoppingToken), evt);
                    break;

                case DetectionEventType.VisitUpdated:
                    foreach (var detectedThrow in _visitDiffer.Diff(evt.Visit ?? []))
                        await Run(executor.RecordThrow(detectedThrow, stoppingToken), evt);
                    break;

                case DetectionEventType.EndOfTurn:
                    _visitDiffer.Reset();
                    await Run(executor.RecordEndOfTurn(stoppingToken), evt);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(evt), evt.Type, "Unknown detection event type.");
            }
        }

        async Task Run(Task<ErrorOr<MatchStateSnapshotDto>> pending, DetectionEvent evt)
        {
            var result = await pending;
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
