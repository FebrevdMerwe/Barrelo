using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Commands.Detection.RecordEndOfTurn;

public sealed record RecordEndOfTurnCommand : IRequest<ErrorOr<MatchStateSnapshotDto>>;
