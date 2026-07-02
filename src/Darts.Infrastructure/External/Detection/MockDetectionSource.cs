using System.Threading.Channels;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Notifications;
using Darts.GameSdk;

namespace Darts.Infrastructure.External.Detection;

/// <summary>Programmatic detection source with no network involved — used directly by tests and the Phase-1 demo harness.</summary>
public sealed class MockDetectionSource : IDetectionSource
{
    private readonly Channel<DetectionEvent> _channel = Channel.CreateUnbounded<DetectionEvent>();

    public void SimulateThrow(DetectedThrow detectedThrow) =>
        _channel.Writer.TryWrite(new DetectionEvent(DetectionEventType.Throw, detectedThrow.BoardId, detectedThrow));

    public void SimulateEndOfTurn(string boardId) =>
        _channel.Writer.TryWrite(new DetectionEvent(DetectionEventType.EndOfTurn, boardId, null));

    public IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public Task<bool> IsConnectedAsync() => Task.FromResult(true);
}
