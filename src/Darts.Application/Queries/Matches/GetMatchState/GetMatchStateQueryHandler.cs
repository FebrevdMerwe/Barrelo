using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Services;
using ErrorOr;

namespace Darts.Application.Queries.Matches.GetMatchState;

public sealed class GetMatchStateQueryHandler(IGameSessionManager sessionManager, ISessionLeaderboardStore leaderboardStore)
    : IRequestHandler<GetMatchStateQuery, ErrorOr<MatchStateSnapshotDto>>
{
    public async Task<ErrorOr<MatchStateSnapshotDto>> Handle(GetMatchStateQuery request, CancellationToken ct)
    {
        var game = await sessionManager.TryGetAsync(request.MatchId);
        if (game is null)
            return MatchSessionErrors.SessionNotFound(request.MatchId);

        var state = await game.GetState();
        var stamped = state with { MatchId = request.MatchId };
        var leaderboard = stamped.IsComplete ? leaderboardStore.GetStandings() : null;
        return MatchStateSnapshotDto.From(stamped, leaderboard);
    }
}
