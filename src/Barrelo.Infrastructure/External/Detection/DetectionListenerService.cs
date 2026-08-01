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
///
/// Nothing that happens downstream of an event is allowed to end this loop. A faulted BackgroundService
/// takes the whole host down by default, and even where it doesn't, a listener that has stopped consuming
/// is a board that quietly scores nothing for the rest of the evening with no visible error. So the
/// stream is consumed under a restart loop, and each event under its own catch: a game plugin that throws
/// costs one dart, not the night.
/// </summary>
public sealed class DetectionListenerService(
    IDetectionSource detectionSource,
    IDetectionStatusNotifier statusNotifier,
    IServiceScopeFactory scopeFactory,
    ILogger<DetectionListenerService> logger)
    : BackgroundService
{
    private static readonly TimeSpan StreamRestartDelay = TimeSpan.FromSeconds(1);

    // Detectors that report a whole visit at a time are reduced to per-dart events here, in one place, so
    // IGame never needs a second way to be told about a throw.
    private readonly VisitDiffer _visitDiffer = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Consume(stoppingToken);
                return; // The source completed its stream — it is shutting down, and so are we.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Detection stream faulted; resubscribing in {Delay}.", StreamRestartDelay);

                // The visit differ is deliberately *not* reset here. Its remembered prefix is the only
                // thing that stops a board manager's post-reconnect resend of the current visit being
                // recorded a second time.
                try { await Task.Delay(StreamRestartDelay, stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private async Task Consume(CancellationToken ct)
    {
        await foreach (var evt in detectionSource.EventsAsync(ct))
        {
            try
            {
                await Handle(evt, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "Failed to handle {EventType} from board '{BoardId}'; the stream continues.",
                    evt.Type, evt.BoardId);
            }
        }
    }

    private async Task Handle(DetectionEvent evt, CancellationToken ct)
    {
        if (evt.Type == DetectionEventType.ConnectionChanged)
        {
            await OnConnectionChanged(evt, ct);
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<GameCommandExecutor>();

        switch (evt.Type)
        {
            case DetectionEventType.Throw:
                await Run(executor.RecordThrow(evt.Throw!, ct), evt);
                break;

            case DetectionEventType.VisitUpdated:
                var diff = _visitDiffer.Diff(evt.Visit ?? []);

                // The board has fewer darts on it than last time and no takeout ever arrived, so the
                // takeout fell in a reconnect gap. Left alone, the game still has the previous player at
                // the oche on a full visit and refuses every dart of this one as a fourth dart — the match
                // stops dead. Closing the turn here is what lets play carry on across a board that
                // dropped and came back.
                if (diff.StartedNewVisit)
                {
                    logger.LogInformation(
                        "Board '{BoardId}' cleared without a takeout event; ending the turn before recording the new visit.",
                        evt.BoardId);
                    await Run(executor.RecordEndOfTurn(ct), evt);
                }

                foreach (var detectedThrow in diff.NewThrows)
                    await Run(executor.RecordThrow(detectedThrow, ct), evt);
                break;

            case DetectionEventType.EndOfTurn:
                _visitDiffer.Reset();
                await Run(executor.RecordEndOfTurn(ct), evt);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(evt), evt.Type, "Unknown detection event type.");
        }
    }

    private async Task OnConnectionChanged(DetectionEvent evt, CancellationToken ct)
    {
        var connected = evt.IsConnected ?? false;
        logger.LogInformation("Board '{BoardId}' is now {State}.", evt.BoardId, connected ? "connected" : "disconnected");

        // No differ reset on either edge: on the way down the visit is still on the board, and on the way
        // up the detector resends it — the remembered prefix is what tells those darts apart from new ones.
        await statusNotifier.NotifyStatusChanged(new DetectionStatus(detectionSource.SourceType, connected), ct);
    }

    private async Task Run(Task<ErrorOr<MatchStateSnapshotDto>> pending, DetectionEvent evt)
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
