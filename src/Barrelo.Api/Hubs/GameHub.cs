using Microsoft.AspNetCore.SignalR;

namespace Barrelo.Api.Hubs;

public sealed class GameHub : Hub
{
    public Task JoinMatch(Guid matchId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(matchId));

    public static string GroupName(Guid matchId) => $"match-{matchId}";
}
