using System.Threading.Channels;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>Programmatic detection source with no network involved — used directly by tests and the Phase-1 demo harness.</summary>
public sealed class MockDetectionSource : IDetectionSource
{
    private readonly Channel<DetectionEvent> _channel = Channel.CreateUnbounded<DetectionEvent>();

    public void SimulateThrow(DetectedThrow detectedThrow) =>
        Simulate(new DetectionEvent(DetectionEventType.Throw, detectedThrow.BoardId, detectedThrow));

    public void SimulateEndOfTurn(string boardId) =>
        Simulate(new DetectionEvent(DetectionEventType.EndOfTurn, boardId, null));

    /// <summary>Pushes an event of any shape, so the consumer's handling of the whole-visit and
    /// connection-change cases can be exercised without a socket.</summary>
    public void Simulate(DetectionEvent evt) => _channel.Writer.TryWrite(evt);

    public IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public DetectionSourceType SourceType => DetectionSourceType.Mock;

    /// <summary>Nothing to connect to, so nothing can drop — this source is always available. A screen
    /// reads that together with <see cref="SourceType"/> and says "manual entry, no board".</summary>
    public Task<bool> IsConnectedAsync() => Task.FromResult(true);
}
