namespace Barrelo.GameSdk;

/// <summary>
/// A point in board-relative, normalized dartboard space — independent of any rendering
/// technology's pixel/viewBox scale.
/// Origin (0,0) is the center of the bullseye. Positive X is rightward, positive Y is upward,
/// in the board's normal mounted, face-on orientation (standard math orientation, not screen/SVG y-down).
/// Segment 20's wedge center lies on the +Y axis (angle 0); angle increases clockwise, matching
/// dartboard.js's NUMBERS ordering.
/// Magnitude 1.0 is the outer edge of the double ring. Magnitude > 1.0 is valid for a genuine miss —
/// callers must not clamp to [-1,1].
/// </summary>
public sealed record BoardPosition(double X, double Y);
