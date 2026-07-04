using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;

namespace Darts.Application.Common.Notifications;

public sealed record GameStateChangedEvent(Guid MatchId, MatchStateSnapshotDto Snapshot) : INotification;
