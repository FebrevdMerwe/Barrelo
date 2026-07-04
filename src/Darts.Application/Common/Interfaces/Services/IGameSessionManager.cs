using Darts.GameSdk;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>
/// Owns the live IGame instances for in-progress matches and the per-matchId locking around them.
/// At most one match is ever active at a time (single board, single manual station). Starting a new
/// session always evicts whatever was previously active — a stuck or abandoned match never blocks a
/// new one from starting. Not persisted/rehydrated across process restart — an interrupted match is
/// lost (explicit v1 limitation).
/// </summary>
public interface IGameSessionManager
{
    /// <summary>The active match's id, or null if no match is currently active.</summary>
    Task<Guid?> TryGetActiveMatchIdAsync();

    /// <summary>
    /// Installs game as the session for matchId and marks it the active match, evicting whatever match
    /// was previously active (its IGame instance is left in place, just no longer reachable via
    /// TryGetActiveMatchIdAsync). playerGroups (playerId -> groupIndex) is retained for the lifetime of
    /// the process so callers can later recover team/group membership for a completed match — e.g. to
    /// resolve placement ties from GameResult.FinalStandings — without the game plugin needing to expose it.
    /// </summary>
    Task StartSessionAsync(Guid matchId, IGame game, IReadOnlyDictionary<Guid, int> playerGroups);

    Task<IGame?> TryGetAsync(Guid matchId);

    /// <summary>The playerId -> groupIndex mapping passed to StartSessionAsync for matchId, or null if unknown.</summary>
    Task<IReadOnlyDictionary<Guid, int>?> TryGetPlayerGroupsAsync(Guid matchId);

    /// <summary>Serializes all mutation of a given match's IGame across concurrent producers.</summary>
    Task<IAsyncDisposable> LockAsync(Guid matchId, CancellationToken ct);

    /// <summary>
    /// Frees the active-match slot (a no-op if matchId isn't the current active match) so a new match
    /// can be started. Does not remove the finished IGame from TryGetAsync's backing store — a
    /// completed match's final state must still be readable.
    /// </summary>
    Task EndActiveSessionAsync(Guid matchId);
}
