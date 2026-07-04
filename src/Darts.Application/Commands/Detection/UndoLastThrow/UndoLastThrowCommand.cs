using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using ErrorOr;

namespace Darts.Application.Commands.Detection.UndoLastThrow;

public sealed record UndoLastThrowCommand : IRequest<ErrorOr<MatchStateSnapshotDto>>;
