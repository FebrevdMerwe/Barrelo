namespace Barrelo.Games.AroundTheClock;

/// <summary>
/// One team's shared position on the clock. <see cref="NextMemberIndex"/> is per-group rather than
/// global because turn order is a round-robin over teams, not over players: every team gets exactly one
/// visit per round and rotates its own thrower, so a three-player team never out-throws a two-player one.
/// </summary>
internal sealed class AroundTheClockGroupState(int groupIndex, IReadOnlyList<Guid> memberPlayerIds)
{
    public int GroupIndex { get; } = groupIndex;
    public IReadOnlyList<Guid> MemberPlayerIds { get; } = memberPlayerIds;

    /// <summary>0 through 21 — see <see cref="AroundTheClockTargets"/>.</summary>
    public int Progress { get; set; }

    public int NextMemberIndex { get; set; }

    public bool IsFinished => Progress >= AroundTheClockTargets.TotalSteps;
}
