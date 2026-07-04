using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Leaderboard;
using Barrelo.Application.Common.Notifications;
using Barrelo.Application.Queries.Players.ListPlayers;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Common.GameExecution;

/// <summary>
/// Shared skeleton behind all three detection commands (throw / end-turn / undo): resolve the single
/// active match, take its lock, run the IGame call, publish the updated snapshot. Factored out once so
/// this sequence isn't copy-pasted three times.
/// </summary>
public sealed class GameCommandExecutor(
    IGameSessionManager sessionManager,
    IDispatcher dispatcher,
    ISessionLeaderboardStore leaderboardStore)
{
    public Task<ErrorOr<MatchStateSnapshotDto>> RecordThrow(DetectedThrow detectedThrow, CancellationToken ct) =>
        RunLocked((_, game, c) => game.ReceiveThrow(detectedThrow, c), ct);

    public Task<ErrorOr<MatchStateSnapshotDto>> RecordEndOfTurn(CancellationToken ct) =>
        RunLocked((_, game, c) => game.ReceiveEndOfTurn(c), ct);

    public Task<ErrorOr<MatchStateSnapshotDto>> Undo(CancellationToken ct) =>
        RunLocked((_, game, c) => game.UndoLastThrow(c), ct);

    private async Task<ErrorOr<MatchStateSnapshotDto>> RunLocked(
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

        IReadOnlyList<LeaderboardEntry>? leaderboard = null;
        if (state.IsComplete)
        {
            leaderboard = await AwardPoints(matchId.Value, game, ct);
            await sessionManager.EndActiveSessionAsync(matchId.Value);
        }

        var dto = MatchStateSnapshotDto.From(stamped, leaderboard);
        await dispatcher.Publish(new GameStateChangedEvent(matchId.Value, dto), ct);

        return dto;
    }

    private async Task<IReadOnlyList<LeaderboardEntry>> AwardPoints(Guid matchId, IGame game, CancellationToken ct)
    {
        var result = await game.GetResult();
        var playerGroups = await sessionManager.TryGetPlayerGroupsAsync(matchId) ?? new Dictionary<Guid, int>();
        var pointsByPlayer = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        if (pointsByPlayer.Count > 0)
        {
            var players = await dispatcher.Send(new ListPlayersQuery(), ct);
            var namesById = players.ToDictionary(p => p.Id, p => p.Name);
            var awards = pointsByPlayer
                .Select(kv => new LeaderboardEntry(kv.Key, namesById.GetValueOrDefault(kv.Key, "Unknown player"), kv.Value))
                .ToList();
            leaderboardStore.RecordResult(matchId, awards);
        }

        return leaderboardStore.GetStandings();
    }
}
