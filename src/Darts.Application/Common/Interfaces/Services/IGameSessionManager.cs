using Darts.GameSdk;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>
/// Owns the live IGame instances for in-progress matches and the per-matchId locking around them.
/// At most one match is ever active at a time (single board, single manual station); TryStartSessionAsync
/// enforces that invariant. Not persisted/rehydrated across process restart — an interrupted match is
/// lost (explicit v1 limitation).
/// </summary>
public interface IGameSessionManager
{
    /// <summary>The active match's id, or null if no match is currently active.</summary>
    Task<Guid?> TryGetActiveMatchIdAsync();

    /// <summary>
    /// Installs game as the session for matchId and marks it the active match, but only if no match is
    /// currently active. Returns false without installing anything if one already is.
    /// </summary>
    Task<bool> TryStartSessionAsync(Guid matchId, IGame game);

    Task<IGame?> TryGetAsync(Guid matchId);

    /// <summary>Serializes all mutation of a given match's IGame across concurrent producers.</summary>
    Task<IAsyncDisposable> LockAsync(Guid matchId, CancellationToken ct);

    /// <summary>
    /// Frees the active-match slot (a no-op if matchId isn't the current active match) so a new match
    /// can be started. Does not remove the finished IGame from TryGetAsync's backing store — a
    /// completed match's final state must still be readable.
    /// </summary>
    Task EndActiveSessionAsync(Guid matchId);
}
