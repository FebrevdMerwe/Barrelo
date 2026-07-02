using Darts.Domain.Entities;
using Darts.Domain.Enums;

namespace Darts.Application.Common.Interfaces.Persistence;

public interface IMatchRepository
{
    Task Add(Match match, CancellationToken ct);

    Task<Match?> GetById(Guid id, CancellationToken ct);

    /// <summary>Assigns the next Sequence for the match internally and persists the resulting ThrowRecord.</summary>
    Task<ThrowRecord> AddThrowRecord(
        Guid matchId,
        Guid playerId,
        int setNumber,
        int legNumber,
        int segment,
        Ring ring,
        int score,
        string rawNotation,
        DetectionSource source,
        DateTimeOffset detectedAtUtc,
        CancellationToken ct);

    /// <summary>Removes the ThrowRecord with the highest Sequence for the match — the counterpart to IGame.UndoLastThrow.</summary>
    Task RemoveLastThrowRecord(Guid matchId, CancellationToken ct);

    Task<IReadOnlyList<ThrowRecord>> GetThrowRecords(Guid matchId, CancellationToken ct);
}
