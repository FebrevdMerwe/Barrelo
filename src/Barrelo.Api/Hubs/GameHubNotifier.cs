using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace Barrelo.Api.Hubs;

public sealed class GameHubNotifier(IHubContext<GameHub> hubContext) : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, MatchStateSnapshotDto snapshot, CancellationToken ct) =>
        hubContext.Clients.All.SendAsync("GameStateUpdated", snapshot, ct);
}
