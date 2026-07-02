using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Errors;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Queries.Matches.GetMatchState;

public sealed class GetMatchStateQueryHandler(IGameSessionManager sessionManager)
    : IRequestHandler<GetMatchStateQuery, ErrorOr<GameStateSnapshot>>
{
    public async Task<ErrorOr<GameStateSnapshot>> Handle(GetMatchStateQuery request, CancellationToken ct)
    {
        var game = await sessionManager.TryGetAsync(request.MatchId);
        if (game is null)
            return MatchSessionErrors.SessionNotFound(request.MatchId);

        var state = await game.GetState();
        return state with { MatchId = request.MatchId };
    }
}
