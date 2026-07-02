using Darts.Application.Common.Constants;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Commands.Detection.RecordEndOfTurn;

public sealed class RecordEndOfTurnCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<RecordEndOfTurnCommand, ErrorOr<GameStateSnapshot>>
{
    public Task<ErrorOr<GameStateSnapshot>> Handle(RecordEndOfTurnCommand request, CancellationToken ct) =>
        executor.RecordEndOfTurn(request.BoardId ?? WellKnownBoardIds.Manual, ct);
}
