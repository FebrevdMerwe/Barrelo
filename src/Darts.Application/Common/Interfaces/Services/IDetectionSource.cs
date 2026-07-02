using Darts.Application.Common.Notifications;

namespace Darts.Application.Common.Interfaces.Services;

public interface IDetectionSource
{
    IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);

    Task<bool> IsConnectedAsync();
}
