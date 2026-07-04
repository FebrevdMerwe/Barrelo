using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Notifications;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Common.GameExecution;

/// <summary>
/// Shared skeleton behind all three detection commands (throw / end-turn / undo): resolve the single
/// active match, take its lock, run the IGame call, publish the updated snapshot. Factored out once so
/// this sequence isn't copy-pasted three times.
/// </summary>
public sealed class GameCommandExecutor(
    IGameSessionManager sessionManager,
    IDispatcher dispatcher)
{
    public Task<ErrorOr<GameStateSnapshot>> RecordThrow(DetectedThrow detectedThrow, CancellationToken ct) =>
        RunLocked((_, game, c) => game.ReceiveThrow(detectedThrow, c), ct);

    public Task<ErrorOr<GameStateSnapshot>> RecordEndOfTurn(CancellationToken ct) =>
        RunLocked((_, game, c) => game.ReceiveEndOfTurn(c), ct);

    public Task<ErrorOr<GameStateSnapshot>> Undo(CancellationToken ct) =>
        RunLocked((_, game, c) => game.UndoLastThrow(c), ct);

    private async Task<ErrorOr<GameStateSnapshot>> RunLocked(
        Func<Guid, IGame, CancellationToken, Task> action,
        CancellationToken ct)
    {
        var matchId = await sessionManager.TryGetActiveMatchIdAsync();
        if (matchId is null)
            return MatchSessionErrors.NoActiveMatch;

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

        if (state.IsComplete)
            await sessionManager.EndActiveSessionAsync(matchId.Value);

        await dispatcher.Publish(new GameStateChangedEvent(matchId.Value, stamped), ct);

        return stamped;
    }
}
