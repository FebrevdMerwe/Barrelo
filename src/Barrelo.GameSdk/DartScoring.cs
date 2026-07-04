namespace Barrelo.GameSdk;

/// <summary>Ring/segment scoring math shared by every game plugin and by manual-entry input. Mirrors mockup/dartboard.js's DartScoring for parity with the reference UI.</summary>
public static class DartScoring
{
    public static int Score(Ring ring, int segment) => ring switch
    {
        Ring.Miss => 0,
        Ring.InnerBull => 50,
        Ring.OuterBull => 25,
        Ring.Double => segment * 2,
        Ring.Triple => segment * 3,
        _ => segment,
    };

    public static string Notation(Ring ring, int segment) => ring switch
    {
        Ring.Miss => "MISS",
        Ring.InnerBull => "BULL",
        Ring.OuterBull => "25",
        Ring.Double => $"D{segment}",
        Ring.Triple => $"T{segment}",
        _ => segment.ToString(),
    };

    /// <summary>A valid double-out checkout finisher: an outer double ring, or the inner bull (50). The outer bull (25) is not a double.</summary>
    public static bool IsValidCheckoutRing(Ring ring) => ring is Ring.Double or Ring.InnerBull;
}
