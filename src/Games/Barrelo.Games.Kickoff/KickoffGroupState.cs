namespace Barrelo.Games.Kickoff;

internal sealed class KickoffGroupState(int groupIndex, IReadOnlyList<Guid> memberPlayerIds)
{
    public int GroupIndex { get; } = groupIndex;
    public IReadOnlyList<Guid> MemberPlayerIds { get; } = memberPlayerIds;
    public int Goals { get; set; }
    public int LegsWon { get; set; }

    /// <summary>Index into <see cref="MemberPlayerIds"/> of whichever member kicks next for this side.</summary>
    public int NextMemberIndex { get; set; }
}
