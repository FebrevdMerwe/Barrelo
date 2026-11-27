using System.Reflection;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.Detection;

public class AutoDartsDetectionSourceTests
{
    [Fact]
    public async Task Parses_throwDetected_events_with_flattened_throw_data()
    {
        const string json = """
            {
              "type": "state",
              "data": {
                "connected": true,
                "event": "throwDetected",
                "running": true,
                "status": "throw",
                "throws": [
                  {
                    "bed": "SingleInner",
                    "bouncer": false,
                    "coordsX": 0.16465158790950485,
                    "coordsY": 0.2873328180894392,
                    "failed": false,
                    "method": "UnanimousCam",
                    "multiplier": 1,
                    "segmentName": "S18",
                    "segmentNumber": 18
                  }
                ]
              }
            }
            """;

        await using var source = CreateSource();
        var events = ParseMessage(source, json);

        events.Should().ContainSingle();
        events[0].Type.Should().Be(DetectionEventType.VisitUpdated);
        events[0].BoardId.Should().Be("autodarts");
        events[0].Visit.Should().ContainSingle();

        var dart = events[0].Visit![0];
        dart.Segment.Should().Be(18);
        dart.Ring.Should().Be(Ring.InnerSingle);
        dart.Score.Should().Be(18);
        dart.RawNotation.Should().Be("18");
        dart.Position.X.Should().BeApproximately(0.16465158790950485, 1e-12);
        dart.Position.Y.Should().BeApproximately(0.2873328180894392, 1e-12);
    }

    [Fact]
    public async Task Maps_takeoutStarted_to_no_event_and_takeoutFinished_to_end_of_turn()
    {
        const string takeoutStarted = """
            {
              "type": "state",
              "data": {
                "event": "takeoutStarted",
                "throws": [
                  {
                    "bed": "SingleInner",
                    "coordsX": -0.1330656085001103,
                    "coordsY": 0.46789361155041775,
                    "segmentName": "S5",
                    "segmentNumber": 5
                  },
                  {
                    "bed": "SingleOuter",
                    "coordsX": 0.6434164432350346,
                    "coordsY": -0.09315668902290805,
                    "segmentName": "S6",
                    "segmentNumber": 6
                  },
                  {
                    "bed": "SingleInner",
                    "coordsX": 0.21255596917105612,
                    "coordsY": 0.00744287205080596,
                    "segmentName": "S6",
                    "segmentNumber": 6
                  }
                ]
              }
            }
            """;
        const string takeoutFinished = """
            {
              "type": "state",
              "data": {
                "event": "takeoutFinished",
                "throws": []
              }
            }
            """;

        await using var source = CreateSource();

        ParseMessage(source, takeoutStarted).Should().BeEmpty();
        var events = ParseMessage(source, takeoutFinished);

        events.Should().ContainSingle();
        events[0].Type.Should().Be(DetectionEventType.EndOfTurn);
    }

    [Fact]
    public async Task Treats_nonempty_takeoutFinished_as_a_visit_update_not_a_turn_boundary()
    {
        const string json = """
            {
              "type": "state",
              "data": {
                "event": "takeoutFinished",
                "throws": [
                  {
                    "bed": "SingleInner",
                    "coordsX": -0.5304558306793381,
                    "coordsY": 0.011671173022713699,
                    "segmentName": "S11",
                    "segmentNumber": 11
                  }
                ]
              }
            }
            """;

        await using var source = CreateSource();
        var events = ParseMessage(source, json);

        events.Should().ContainSingle();
        events[0].Type.Should().Be(DetectionEventType.VisitUpdated);
        events[0].Visit.Should().ContainSingle();
        events[0].Visit![0].Segment.Should().Be(11);
    }

    private static AutoDartsDetectionSource CreateSource() =>
        new(new Uri("ws://127.0.0.1:1/api/events"), "autodarts", NullLogger<AutoDartsDetectionSource>.Instance);

    private static List<DetectionEvent> ParseMessage(AutoDartsDetectionSource source, string json)
    {
        var parseMessage = typeof(AutoDartsDetectionSource).GetMethod(
            "ParseMessage", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IEnumerable<DetectionEvent>)parseMessage.Invoke(source, [json])!).ToList();
    }
}
