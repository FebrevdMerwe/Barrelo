using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock;

/// <summary>
/// The clock's fixed 21-step ladder: the numbers 1 through 20 in order, then the bull last. A team's
/// position on it is a single integer, so "which target am I on" is derived rather than stored.
/// </summary>
internal static class AroundTheClockTargets
{
    /// <summary>The position a team occupies once every number is behind them and only the bull is left.</summary>
    public const int BullStep = 20;

    /// <summary>Progress at or above this means the bull has been hit and the team has finished.</summary>
    public const int TotalSteps = 21;

    /// <summary>The number a team on this step must hit, or null once they're on the bull (or finished).</summary>
    public static int? TargetNumberFor(int progress) => progress < BullStep ? progress + 1 : null;

    /// <summary>
    /// How far a ring carries you up the ladder when it lands on your number: a treble jumps three
    /// numbers, a double two, any single one. Never called on the bull step — the bull is ring-specific
    /// and handled separately.
    /// </summary>
    public static int StepsFor(Ring ring) => ring switch
    {
        Ring.Triple => 3,
        Ring.Double => 2,
        Ring.InnerSingle or Ring.OuterSingle or Ring.Single => 1,
        _ => 0,
    };
}
