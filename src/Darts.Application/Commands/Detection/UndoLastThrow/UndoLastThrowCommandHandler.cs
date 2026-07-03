using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Commands.Detection.UndoLastThrow;

public sealed class UndoLastThrowCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<UndoLastThrowCommand, ErrorOr<GameStateSnapshot>>
{
    public Task<ErrorOr<GameStateSnapshot>> Handle(UndoLastThrowCommand request, CancellationToken ct) =>
        executor.Undo(ct);
}
