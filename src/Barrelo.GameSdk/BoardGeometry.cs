namespace Barrelo.GameSdk;

/// <summary>
/// Single source of truth for "segment/ring → normalized board-space center point," used by every
/// detection source that only knows the resolved segment/ring and needs a deterministic BoardPosition
/// to satisfy DetectedThrow's mandatory Position (Manual entry, MockDetectionSource, test fixtures, and
/// BoardSimulatorDetectionSource's fallback when no real click coordinate is available).
/// Mirrors the NUMBERS ordering and R radii in dartboard.js, scaled into the normalized space
/// documented on BoardPosition (divide every radius by R.doubleOut).
/// </summary>
public static class BoardGeometry
{
    // Same clockwise-from-top ordering as NUMBERS in dartboard.js.
    private static readonly int[] SegmentOrder =
        [20, 1, 18, 4, 13, 6, 10, 15, 2, 17, 3, 19, 7, 16, 8, 11, 14, 9, 12, 5];

    // Same proportions as R in dartboard.js, divided by R.doubleOut (100) into normalized units.
    private const double BullInner = 6.0 / 100.0;
    private const double BullOuter = 15.0 / 100.0;
    private const double TripleInner = 58.0 / 100.0;
    private const double TripleOuter = 64.0 / 100.0;
    private const double DoubleInner = 94.0 / 100.0;
    private const double DoubleOuter = 1.0;

    // Miss has no wedge/ring band in dartboard.js at all; use a nominal point just outside the double
    // ring. Angle is arbitrary (no segment is meaningful for a miss) — pinned to 0 (top) for determinism.
    private const double MissRadius = 1.05;

    /// <summary>
    /// Deterministic geometric center of a segment/ring wedge: angular center of the segment, radial
    /// midpoint of the ring band. Ignores <paramref name="segment"/> for Miss/InnerBull/OuterBull, which
    /// have no wedge. Throws for segment values outside 1-20 when ring requires a wedge.
    /// </summary>
    public static BoardPosition CenterOf(int segment, Ring ring)
    {
        var (inner, outer) = RadialBand(ring);
        var radius = (inner + outer) / 2.0;
        var angleDeg = ring is Ring.Miss or Ring.InnerBull or Ring.OuterBull ? 0.0 : AngleForSegment(segment);
        return FromPolar(radius, angleDeg);
    }

    private static (double Inner, double Outer) RadialBand(Ring ring) => ring switch
    {
        Ring.Miss => (MissRadius, MissRadius),
        Ring.InnerBull => (0.0, BullInner),
        Ring.OuterBull => (BullInner, BullOuter),
        Ring.Inner => (BullOuter, TripleInner),
        Ring.Triple => (TripleInner, TripleOuter),
        Ring.Outer => (TripleOuter, DoubleInner),
        Ring.Double => (DoubleInner, DoubleOuter),
        _ => throw new ArgumentOutOfRangeException(nameof(ring), ring, null),
    };

    private static double AngleForSegment(int segment)
    {
        var index = Array.IndexOf(SegmentOrder, segment);
        if (index < 0)
            throw new ArgumentOutOfRangeException(nameof(segment), segment, "Segment must be 1-20.");
        return index * 18.0;
    }

    private static BoardPosition FromPolar(double radius, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new BoardPosition(radius * Math.Sin(rad), radius * Math.Cos(rad));
    }
}
