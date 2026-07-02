using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Notifications;
using Darts.GameSdk;
using ErrorOr;
using DomainRing = Darts.Domain.Enums.Ring;
using DomainSource = Darts.Domain.Enums.DetectionSource;

namespace Darts.Application.Common.GameExecution;

/// <summary>
/// Shared skeleton behind all three detection commands (throw / end-turn / undo): resolve MatchId from
/// BoardId, take the per-match lock, run the IGame call, persist the resulting ThrowRecord change, publish
/// the updated snapshot, and save. Factored out once so this sequence isn't copy-pasted three times.
/// </summary>
public sealed class GameCommandExecutor(
    IGameSessionManager sessionManager,
    IMatchRepository matchRepository,
    IUnitOfWork unitOfWork,
    IDispatcher dispatcher)
{
    public Task<ErrorOr<GameStateSnapshot>> RecordThrow(string boardId, DetectedThrow detectedThrow, CancellationToken ct) =>
        RunLocked(boardId, async (matchId, game, c) =>
        {
            var preState = await game.GetState();
            await game.ReceiveThrow(detectedThrow, c);

            if (preState.CurrentPlayerId is { } throwingPlayerId)
            {
                await matchRepository.AddThrowRecord(
                    matchId,
                    throwingPlayerId,
                    preState.SetNumber,
                    preState.LegNumber,
                    detectedThrow.Segment,
                    MapRing(detectedThrow.Ring),
                    detectedThrow.Score,
                    detectedThrow.RawNotation,
                    MapSource(detectedThrow.Source),
                    detectedThrow.DetectedAtUtc,
                    c);
            }
        }, ct);

    public Task<ErrorOr<GameStateSnapshot>> RecordEndOfTurn(string boardId, CancellationToken ct) =>
        RunLocked(boardId, (_, game, c) => game.ReceiveEndOfTurn(c), ct);

    public Task<ErrorOr<GameStateSnapshot>> Undo(string boardId, CancellationToken ct) =>
        RunLocked(boardId, async (matchId, game, c) =>
        {
            await game.UndoLastThrow(c);
            await matchRepository.RemoveLastThrowRecord(matchId, c);
        }, ct);

    private async Task<ErrorOr<GameStateSnapshot>> RunLocked(
        string boardId,
        Func<Guid, IGame, CancellationToken, Task> action,
        CancellationToken ct)
    {
        var matchId = sessionManager.ResolveMatchForBoard(boardId);
        if (matchId is null)
            return MatchSessionErrors.BoardNotBound(boardId);

        await using var _ = await sessionManager.LockAsync(matchId.Value, ct);

        var game = await sessionManager.TryGetAsync(matchId.Value);
        if (game is null)
            return MatchSessionErrors.SessionNotFound(matchId.Value);

        try
        {
            await action(matchId.Value, game, ct);
        }
        catch (GameRuleViolationException ex)
        {
            return Error.Validation("Game.RuleViolation", ex.Message);
        }

        var state = await game.GetState();
        var stamped = state with { MatchId = matchId.Value };

        await unitOfWork.SaveChangesAsync(ct);
        await dispatcher.Publish(new GameStateChangedEvent(matchId.Value, stamped), ct);

        return stamped;
    }

    private static DomainRing MapRing(Ring ring) => ring switch
    {
        Ring.Miss => DomainRing.Miss,
        Ring.Inner => DomainRing.Inner,
        Ring.Outer => DomainRing.Outer,
        Ring.Triple => DomainRing.Triple,
        Ring.Double => DomainRing.Double,
        Ring.InnerBull => DomainRing.InnerBull,
        Ring.OuterBull => DomainRing.OuterBull,
        _ => throw new ArgumentOutOfRangeException(nameof(ring), ring, null),
    };

    private static DomainSource MapSource(DetectionSourceType source) => source switch
    {
        DetectionSourceType.AutoDarts => DomainSource.AutoDarts,
        DetectionSourceType.Mock => DomainSource.Mock,
        DetectionSourceType.Manual => DomainSource.Manual,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
    };
}
