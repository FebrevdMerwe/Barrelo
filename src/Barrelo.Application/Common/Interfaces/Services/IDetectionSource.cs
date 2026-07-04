using Barrelo.Application.Common.Notifications;

namespace Barrelo.Application.Common.Interfaces.Services;

public interface IDetectionSource
{
    IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);

    Task<bool> IsConnectedAsync();
}
