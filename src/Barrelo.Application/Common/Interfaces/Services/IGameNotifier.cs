using Barrelo.Application.Common.GameExecution;

namespace Barrelo.Application.Common.Interfaces.Services;

/// <summary>Pushes a fresh MatchStateSnapshotDto to whatever live clients are watching a match (SignalR, etc).</summary>
public interface IGameNotifier
{
    Task NotifyStateChanged(Guid matchId, MatchStateSnapshotDto snapshot, CancellationToken ct);
}
