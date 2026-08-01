using Barrelo.Application.Common.Notifications;

namespace Barrelo.Application.Common.Interfaces.Services;

/// <summary>Pushes board connection changes to whatever live clients are watching (SignalR, etc). Separate
/// from IGameNotifier because the board's state is not the match's state — it changes while no match is
/// running, and a screen showing "no match in progress" still needs to say whether the board is there.</summary>
public interface IDetectionStatusNotifier
{
    Task NotifyStatusChanged(DetectionStatus status, CancellationToken ct);
}
