using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;

namespace Barrelo.Application.Common.Notifications;

public sealed record GameStateChangedEvent(Guid MatchId, MatchStateSnapshotDto Snapshot) : INotification;
