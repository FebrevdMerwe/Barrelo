namespace Darts.Games.Cricket;

internal sealed class CricketGroupState(int groupIndex, IReadOnlyList<Guid> memberPlayerIds)
{
    public int GroupIndex { get; } = groupIndex;
    public IReadOnlyList<Guid> MemberPlayerIds { get; } = memberPlayerIds;
    public int[] Marks { get; } = new int[CricketTargets.Count];
    public int Points { get; set; }
    public int ClosedCount => Marks.Count(m => m >= CricketTargets.MarksToClose);
    public bool HasClosedEverything => ClosedCount == CricketTargets.Count;
}
