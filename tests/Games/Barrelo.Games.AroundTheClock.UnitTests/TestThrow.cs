using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock.UnitTests;

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

    /// <summary>The inner bull (50) — the only dart that finishes the clock.</summary>
    public static DetectedThrow InnerBull() => Of(Ring.Double, 25);

    /// <summary>The outer bull ring (25), which counts for nothing in this game.</summary>
    public static DetectedThrow OuterBull() => Of(Ring.Single, 25);
}
