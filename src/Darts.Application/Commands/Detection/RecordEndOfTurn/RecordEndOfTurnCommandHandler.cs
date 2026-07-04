using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using ErrorOr;

namespace Darts.Application.Commands.Detection.RecordEndOfTurn;

public sealed class RecordEndOfTurnCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<RecordEndOfTurnCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public Task<ErrorOr<MatchStateSnapshotDto>> Handle(RecordEndOfTurnCommand request, CancellationToken ct) =>
        executor.RecordEndOfTurn(ct);
}
