using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Services;

namespace Darts.Application.Common.Notifications;

public sealed class GameStateChangedNotificationHandler(IGameNotifier notifier)
    : INotificationHandler<GameStateChangedEvent>
{
    public Task Handle(GameStateChangedEvent notification, CancellationToken ct) =>
        notifier.NotifyStateChanged(notification.MatchId, notification.Snapshot, ct);
}
