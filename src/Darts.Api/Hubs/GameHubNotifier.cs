using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace Darts.Api.Hubs;

public sealed class GameHubNotifier(IHubContext<GameHub> hubContext) : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, MatchStateSnapshotDto snapshot, CancellationToken ct) =>
        hubContext.Clients.Group(GameHub.GroupName(matchId)).SendAsync("GameStateUpdated", snapshot, ct);
}
