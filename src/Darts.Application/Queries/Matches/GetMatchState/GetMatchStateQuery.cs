using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using ErrorOr;

namespace Darts.Application.Queries.Matches.GetMatchState;

public sealed record GetMatchStateQuery(Guid MatchId) : IRequest<ErrorOr<MatchStateSnapshotDto>>;
