using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Commands.Detection.UndoLastThrow;

public sealed class UndoLastThrowCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<UndoLastThrowCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public Task<ErrorOr<MatchStateSnapshotDto>> Handle(UndoLastThrowCommand request, CancellationToken ct) =>
        executor.Undo(ct);
}
