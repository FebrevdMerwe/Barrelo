using Darts.Application.Common.Dispatch;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Queries.Matches.GetMatchState;

public sealed record GetMatchStateQuery(Guid MatchId) : IRequest<ErrorOr<GameStateSnapshot>>;
