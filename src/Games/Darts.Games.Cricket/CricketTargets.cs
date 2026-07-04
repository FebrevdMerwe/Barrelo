using Darts.GameSdk;

namespace Darts.Games.Cricket;

/// <summary>Standard Cricket's fixed target set: 20 down to 15, then Bull last. Index-aligned
/// with every Marks array in this plugin's state.</summary>
internal static class CricketTargets
{
    public static readonly int[] Numbers = [20, 19, 18, 17, 16, 15];
    public const int BullIndex = 6;
    public const int Count = 7; // Numbers.Length + 1 (bull)
    public const int MarksToClose = 3;

    public static int PointValue(int targetIndex) =>
        targetIndex == BullIndex ? 25 : Numbers[targetIndex];

    /// <summary>-1 for Miss or any segment that isn't a Cricket number (e.g. 1-14).</summary>
    public static int IndexFor(Ring ring, int segment)
    {
        if (ring is Ring.InnerBull or Ring.OuterBull) return BullIndex;
        if (ring == Ring.Miss) return -1;
        return Array.IndexOf(Numbers, segment);
    }

    public static int MarksForRing(Ring ring) => ring switch
    {
        Ring.Triple => 3,
        Ring.Double => 2,
        Ring.InnerBull => 2,
        Ring.Inner or Ring.Outer or Ring.OuterBull => 1,
        _ => 0,
    };
}
