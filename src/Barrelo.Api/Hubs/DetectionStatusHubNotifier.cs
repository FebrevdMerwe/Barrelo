using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace Barrelo.Api.Hubs;

/// <summary>Broadcasts board connection changes on the same hub as match state. Singleton, unlike
/// GameHubNotifier: the caller is DetectionListenerService, which is itself a singleton and has no
/// request scope to hang off.</summary>
public sealed class DetectionStatusHubNotifier(IHubContext<GameHub> hubContext) : IDetectionStatusNotifier
{
    public Task NotifyStatusChanged(DetectionStatus status, CancellationToken ct) =>
        hubContext.Clients.All.SendAsync("DetectionStatusChanged", status, ct);
}
