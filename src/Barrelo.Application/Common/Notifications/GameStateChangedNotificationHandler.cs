using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;

namespace Barrelo.Application.Common.Notifications;

public sealed class GameStateChangedNotificationHandler(IGameNotifier notifier)
    : INotificationHandler<GameStateChangedEvent>
{
    public Task Handle(GameStateChangedEvent notification, CancellationToken ct) =>
        notifier.NotifyStateChanged(notification.MatchId, notification.Snapshot, ct);
}
