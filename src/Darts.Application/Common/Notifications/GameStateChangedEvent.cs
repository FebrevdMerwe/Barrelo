using Darts.Application.Common.Dispatch;
using Darts.GameSdk;

namespace Darts.Application.Common.Notifications;

public sealed record GameStateChangedEvent(Guid MatchId, GameStateSnapshot Snapshot) : INotification;
