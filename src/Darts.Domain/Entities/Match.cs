using Darts.Domain.Common;
using Darts.Domain.Enums;
using Darts.Domain.Errors;
using ErrorOr;

namespace Darts.Domain.Entities;

public sealed class Match : Entity<Guid>
{
    public string GameId { get; private set; } = string.Empty;

    public string GameConfigJson { get; private set; } = string.Empty;

    public MatchStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    private readonly List<Guid> _winnerPlayerIds = [];
    public IReadOnlyList<Guid> WinnerPlayerIds => _winnerPlayerIds.AsReadOnly();

    private readonly List<MatchParticipant> _participants = [];
    public IReadOnlyList<MatchParticipant> Participants => _participants.AsReadOnly();

    private Match()
    {
    }

    public static ErrorOr<Match> Start(
        string gameId,
        string gameConfigJson,
        IReadOnlyList<Guid> orderedPlayerIds,
        IReadOnlyList<int>? groupIndexes = null)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return MatchErrors.GameIdRequired;

        if (orderedPlayerIds.Count == 0)
            return MatchErrors.NoParticipants;

        if (groupIndexes is not null && groupIndexes.Count != orderedPlayerIds.Count)
            return MatchErrors.GroupAssignmentMismatch;

        var match = new Match
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            GameConfigJson = gameConfigJson,
            Status = MatchStatus.InProgress,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        for (var order = 0; order < orderedPlayerIds.Count; order++)
        {
            var groupIndex = groupIndexes?[order] ?? order;
            match._participants.Add(new MatchParticipant(orderedPlayerIds[order], order, groupIndex));
        }

        return match;
    }

    public ErrorOr<Updated> Complete(IReadOnlyList<Guid> winnerPlayerIds)
    {
        if (Status == MatchStatus.Completed)
            return MatchErrors.AlreadyCompleted;

        Status = MatchStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        _winnerPlayerIds.Clear();
        _winnerPlayerIds.AddRange(winnerPlayerIds);

        return Result.Updated;
    }
}
