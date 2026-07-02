using Darts.Application.Common.Notifications;
using Darts.GameSdk;
using Darts.Infrastructure.External.Detection;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.Detection;

public class MockDetectionSourceTests
{
    [Fact]
    public async Task Simulated_events_are_observed_in_order_via_EventsAsync()
    {
        var source = new MockDetectionSource();
        var detectedThrow = new DetectedThrow(
            Guid.NewGuid(), 20, Ring.Triple, 60, "T20", null, null, "mock-board", null, DateTimeOffset.UtcNow, DetectionSourceType.Mock);

        source.SimulateThrow(detectedThrow);
        source.SimulateEndOfTurn();

        var events = new List<DetectionEvent>();
        await foreach (var evt in source.EventsAsync(CancellationToken.None))
        {
            events.Add(evt);
            if (events.Count == 2) break;
        }

        events[0].Type.Should().Be(DetectionEventType.Throw);
        events[0].Throw.Should().Be(detectedThrow);
        events[1].Type.Should().Be(DetectionEventType.EndOfTurn);
        events[1].Throw.Should().BeNull();
    }

    [Fact]
    public async Task IsConnectedAsync_always_returns_true()
    {
        var source = new MockDetectionSource();

        (await source.IsConnectedAsync()).Should().BeTrue();
    }
}
