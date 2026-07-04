using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Commands.Detection.UndoLastThrow;

public sealed record UndoLastThrowCommand : IRequest<ErrorOr<MatchStateSnapshotDto>>;
