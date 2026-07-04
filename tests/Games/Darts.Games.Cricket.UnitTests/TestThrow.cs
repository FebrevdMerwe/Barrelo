using Darts.GameSdk;

namespace Darts.Games.Cricket.UnitTests;

internal static class TestThrow
{
    public static DetectedThrow Of(Ring ring, int segment = 0) => new(
        ThrowId: Guid.NewGuid(),
        Segment: segment,
        Ring: ring,
        Score: DartScoring.Score(ring, segment),
        RawNotation: DartScoring.Notation(ring, segment),
        Position: BoardGeometry.CenterOf(segment, ring),
        Confidence: null,
        BoardId: "test-board",
        CameraIndex: null,
        DetectedAtUtc: DateTimeOffset.UtcNow,
        Source: DetectionSourceType.Mock);
}
