using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Services;

namespace Darts.Infrastructure.External.Notifications;

/// <summary>Default no-op IGameNotifier so any host resolves GameStateChangedNotificationHandler even
/// without a live-push transport wired up. Darts.Api overrides this with a real SignalR notifier.</summary>
public sealed class NullGameNotifier : IGameNotifier
{
    public Task NotifyStateChanged(Guid matchId, MatchStateSnapshotDto snapshot, CancellationToken ct) =>
        Task.CompletedTask;
}
