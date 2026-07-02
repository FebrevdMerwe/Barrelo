using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;

namespace Darts.Infrastructure.External.Notifications;

/// <summary>Default no-op IGameNotifier so any host resolves GameStateChangedNotificationHandler even
/// without a live-push transport wired up. Darts.Api overrides this with a real SignalR notifier.</summary>
public sealed class NullGameNotifier : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, GameStateSnapshot snapshot, CancellationToken ct) =>
        Task.CompletedTask;
}
