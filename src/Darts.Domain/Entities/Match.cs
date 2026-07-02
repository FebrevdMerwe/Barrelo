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

    public InputSource InputSource { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset? CompletedAtUtc { get; private set; }

    public Guid? WinnerPlayerId { get; private set; }

    private readonly List<MatchParticipant> _participants = [];
    public IReadOnlyList<MatchParticipant> Participants => _participants.AsReadOnly();

    private Match()
    {
    }

    public static ErrorOr<Match> Start(
        string gameId,
        string gameConfigJson,
        InputSource inputSource,
        IReadOnlyList<Guid> orderedPlayerIds)
    {
        if (string.IsNullOrWhiteSpace(gameId))
            return MatchErrors.GameIdRequired;

        if (orderedPlayerIds.Count == 0)
            return MatchErrors.NoParticipants;

        var match = new Match
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            GameConfigJson = gameConfigJson,
            Status = MatchStatus.InProgress,
            InputSource = inputSource,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        for (var order = 0; order < orderedPlayerIds.Count; order++)
            match._participants.Add(new MatchParticipant(orderedPlayerIds[order], order));

        return match;
    }

    public ErrorOr<Updated> Complete(Guid? winnerPlayerId)
    {
        if (Status == MatchStatus.Completed)
            return MatchErrors.AlreadyCompleted;

        Status = MatchStatus.Completed;
        CompletedAtUtc = DateTimeOffset.UtcNow;
        WinnerPlayerId = winnerPlayerId;

        return Result.Updated;
    }
}
