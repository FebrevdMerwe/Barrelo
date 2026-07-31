using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.Interfaces.Services;
using ErrorOr;

namespace Barrelo.Application.Commands.Matches.ReportReplayHash;

public sealed class ReportReplayHashCommandHandler(
    IGameSessionManager sessionManager,
    IReplayDivergenceMonitor divergenceMonitor)
    : IRequestHandler<ReportReplayHashCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(ReportReplayHashCommand request, CancellationToken ct)
    {
        var matchId = await sessionManager.TryGetActiveMatchIdAsync();
        if (matchId is null)
            return MatchSessionErrors.NoActiveMatch;

        if (string.IsNullOrWhiteSpace(request.LogHash) || string.IsNullOrWhiteSpace(request.StateHash))
            return Error.Validation("Match.EmptyReplayHash", "Both a log hash and a state hash are required.");

        divergenceMonitor.Report(matchId.Value, request.LogHash, request.StateHash);
        return Result.Success;
    }
}
