using Darts.GameSdk;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>
/// Owns the live IGame instances for in-progress matches, per-matchId locking around them, and the
/// BoardId -> MatchId routing that lets detection events reach the right match. Not persisted/rehydrated
/// across process restart — an interrupted match is lost (explicit v1 limitation).
/// </summary>
public interface IGameSessionManager
{
    Task StartSessionAsync(Guid matchId, IGame game);

    Task<IGame?> TryGetAsync(Guid matchId);

    /// <summary>Serializes all mutation of a given match's IGame across concurrent producers.</summary>
    Task<IAsyncDisposable> LockAsync(Guid matchId, CancellationToken ct);

    void BindBoard(string boardId, Guid matchId);

    Guid? ResolveMatchForBoard(string boardId);
}
