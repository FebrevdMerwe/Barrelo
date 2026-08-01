using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;

namespace Barrelo.Infrastructure.External.Notifications;

/// <summary>Default no-op IDetectionStatusNotifier so any host can run DetectionListenerService without a
/// live-push transport wired up. Barrelo.Api overrides this with a real SignalR notifier.</summary>
public sealed class NullDetectionStatusNotifier : IDetectionStatusNotifier
{
    public Task NotifyStatusChanged(DetectionStatus status, CancellationToken ct) => Task.CompletedTask;
}
