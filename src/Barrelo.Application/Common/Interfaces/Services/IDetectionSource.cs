using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Interfaces.Services;

public interface IDetectionSource
{
    /// <summary>Which detector this is. Surfaced so a screen can name the board without anything upstream
    /// having to know which concrete source was configured.</summary>
    DetectionSourceType SourceType { get; }

    IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct);

    Task<bool> IsConnectedAsync();
}
