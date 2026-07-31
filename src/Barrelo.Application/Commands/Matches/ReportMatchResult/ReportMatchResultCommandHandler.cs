using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Commands.Matches.ReportMatchResult;

/// <summary>
/// A reported result is client-authored, so it is checked against the match's actual roster before it
/// reaches the leaderboard — a malformed or stale client should fail loudly rather than award points to
/// players who weren't in the match.
/// </summary>
public sealed class ReportMatchResultCommandHandler(
    GameCommandExecutor executor,
    IGameSessionManager sessionManager)
    : IRequestHandler<ReportMatchResultCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public async Task<ErrorOr<MatchStateSnapshotDto>> Handle(ReportMatchResultCommand request, CancellationToken ct)
    {
        var matchId = await sessionManager.TryGetActiveMatchIdAsync();
        if (matchId is null)
            return MatchSessionErrors.NoActiveMatch;

        var playerGroups = await sessionManager.TryGetPlayerGroupsAsync(matchId.Value);
        if (playerGroups is null)
            return MatchSessionErrors.SessionNotFound(matchId.Value);

        if (request.FinalStandings.Count == 0)
            return Error.Validation("Match.EmptyStandings", "Final standings must list at least one player.");

        if (request.FinalStandings.Distinct().Count() != request.FinalStandings.Count)
            return Error.Validation("Match.DuplicateStandings", "Final standings must not list a player more than once.");

        var participants = playerGroups.Keys.ToHashSet();
        if (!request.FinalStandings.All(participants.Contains))
            return Error.Validation("Match.UnknownPlayer", "Final standings include a player who is not in this match.");

        // LeaderboardPointsCalculator walks FinalStandings and looks every id up in playerGroups, so a
        // winner missing from the standings would be silently unrewarded rather than rejected.
        if (!request.WinnerPlayerIds.All(request.FinalStandings.Contains))
            return Error.Validation("Match.UnknownWinner", "Every winner must also appear in the final standings.");

        return await executor.ReportClientResult(
            new GameResult(request.WinnerPlayerIds, request.FinalStandings), ct);
    }
}
