using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Queries.Matches.GetMatchState;

public sealed record GetMatchStateQuery(Guid MatchId) : IRequest<ErrorOr<MatchStateSnapshotDto>>;
