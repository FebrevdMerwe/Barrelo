using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Services;

namespace Barrelo.Infrastructure.External.Notifications;

/// <summary>Default no-op IGameNotifier so any host resolves GameStateChangedNotificationHandler even
/// without a live-push transport wired up. Barrelo.Api overrides this with a real SignalR notifier.</summary>
public sealed class NullGameNotifier : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, MatchStateSnapshotDto snapshot, CancellationToken ct) =>
        Task.CompletedTask;
}
