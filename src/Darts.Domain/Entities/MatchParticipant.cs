namespace Darts.Domain.Entities;

/// <summary>Owned type with no independent identity — always accessed through its parent Match.</summary>
public sealed class MatchParticipant
{
    public Guid PlayerId { get; private set; }

    public int Order { get; private set; }

    public int GroupIndex { get; private set; }

    public int? FinalPosition { get; private set; }

    private MatchParticipant()
    {
    }

    internal MatchParticipant(Guid playerId, int order, int groupIndex)
    {
        PlayerId = playerId;
        Order = order;
        GroupIndex = groupIndex;
    }

    internal void SetFinalPosition(int position) => FinalPosition = position;
}
