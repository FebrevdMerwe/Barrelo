using Darts.GameSdk;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>Pushes a fresh GameStateSnapshot to whatever live clients are watching a match (SignalR, etc).</summary>
public interface IGameNotifier
{
    Task NotifyStateChanged(Guid matchId, GameStateSnapshot snapshot, CancellationToken ct);
}
