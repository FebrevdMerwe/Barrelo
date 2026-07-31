namespace Barrelo.GameSdk;

/// <summary>Mirrors the ring vocabulary used by the mockup dartboard (mockup/dartboard.js) for consistency with the eventual UI.</summary>
public enum Ring
{
    Miss,
    InnerSingle,
    OuterSingle,
    Triple,
    Double,

    /// <summary>The 25-point bull ring. Always paired with segment 25 — see <see cref="DartScoring.IsBull"/>.
    /// The 50-point bull is <see cref="Double"/> at segment 25, since it genuinely is a double.</summary>
    Single,
}
