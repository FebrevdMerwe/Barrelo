namespace Barrelo.GameSdk;

/// <summary>Ring/segment scoring math shared by every game plugin and by manual-entry input. Mirrors mockup/dartboard.js's DartScoring for parity with the reference UI.</summary>
public static class DartScoring
{
    public static int Score(Ring ring, int segment) => ring switch
    {
        Ring.Miss => 0,
        Ring.Double => segment * 2,
        Ring.Triple => segment * 3,
        _ => segment,
    };

    public static string Notation(Ring ring, int segment)
    {
        if (IsBull(ring, segment)) return ring == Ring.Double ? "BULL" : "25";
        return ring switch
        {
            Ring.Miss => "MISS",
            Ring.Double => $"D{segment}",
            Ring.Triple => $"T{segment}",
            _ => segment.ToString(),
        };
    }

    /// <summary>A valid double-out checkout finisher: any double ring, including the inner bull (50, which is
    /// Ring.Double at segment 25). The outer bull (25, Ring.Single) is not a double.</summary>
    public static bool IsValidCheckoutRing(Ring ring) => ring is Ring.Double;

    /// <summary>Whether this ring/segment combination is the bullseye (25 or 50), the board's only segment
    /// with no wedge. Centralizes the "segment 25 is special" fact so no call site hardcodes it independently.</summary>
    public static bool IsBull(Ring ring, int segment) => segment == 25 && ring is Ring.Single or Ring.Double;
}
