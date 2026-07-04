using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Services;
using ErrorOr;

namespace Darts.Application.Commands.Leaderboard.ResetLeaderboard;

public sealed class ResetLeaderboardCommandHandler(ISessionLeaderboardStore leaderboardStore)
    : IRequestHandler<ResetLeaderboardCommand, ErrorOr<Success>>
{
    public Task<ErrorOr<Success>> Handle(ResetLeaderboardCommand request, CancellationToken ct)
    {
        leaderboardStore.Reset();
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
