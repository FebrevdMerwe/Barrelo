using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;
using Microsoft.AspNetCore.SignalR;

namespace Darts.Api.Hubs;

public sealed class GameHubNotifier(IHubContext<GameHub> hubContext) : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, GameStateSnapshot snapshot, CancellationToken ct) =>
        hubContext.Clients.Group(GameHub.GroupName(matchId)).SendAsync("GameStateUpdated", snapshot, ct);
}
