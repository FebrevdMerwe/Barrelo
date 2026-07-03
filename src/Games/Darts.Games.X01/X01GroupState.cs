namespace Darts.Games.X01;

internal sealed class X01GroupState(int groupIndex, IReadOnlyList<Guid> memberPlayerIds, int startingScore)
{
    public int GroupIndex { get; } = groupIndex;
    public IReadOnlyList<Guid> MemberPlayerIds { get; } = memberPlayerIds;
    public int RemainingScore { get; set; } = startingScore;
    public int LegsWon { get; set; }
    public int SetsWon { get; set; }
}
