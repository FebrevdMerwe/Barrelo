using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Commands.Detection.RecordEndOfTurn;

public sealed class RecordEndOfTurnCommandHandler(GameCommandExecutor executor)
    : IRequestHandler<RecordEndOfTurnCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public Task<ErrorOr<MatchStateSnapshotDto>> Handle(RecordEndOfTurnCommand request, CancellationToken ct) =>
        executor.RecordEndOfTurn(ct);
}
