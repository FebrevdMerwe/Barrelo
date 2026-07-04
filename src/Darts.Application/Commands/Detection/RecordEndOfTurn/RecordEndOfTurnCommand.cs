using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using ErrorOr;

namespace Darts.Application.Commands.Detection.RecordEndOfTurn;

public sealed record RecordEndOfTurnCommand : IRequest<ErrorOr<MatchStateSnapshotDto>>;
