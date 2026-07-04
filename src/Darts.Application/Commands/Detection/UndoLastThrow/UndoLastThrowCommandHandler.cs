using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using ErrorOr;

namespace Darts.Application.Commands.Detection.UndoLastThrow;

public sealed class UndoLastThrowCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<UndoLastThrowCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public Task<ErrorOr<MatchStateSnapshotDto>> Handle(UndoLastThrowCommand request, CancellationToken ct) =>
        executor.Undo(ct);
}
