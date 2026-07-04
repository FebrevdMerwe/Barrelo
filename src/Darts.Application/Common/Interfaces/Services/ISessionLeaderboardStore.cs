using Darts.Application.Common.Leaderboard;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>
/// Session-scoped, in-memory running totals of points awarded across completed matches this session.
/// Not persisted/rehydrated across process restart (same v1 limitation as IGameSessionManager and
/// ISessionPlayerStore). Entries are keyed by player Guid and snapshot the player's name at the moment
/// points were recorded — never a live lookup — so an entry survives a mid-session erase/delete of that
/// player and still displays under their last-known name.
/// </summary>
public interface ISessionLeaderboardStore
{
    /// <summary>
    /// Adds awards (per-match point deltas, already filtered to players who scored more than zero) onto
    /// running totals. Idempotent per matchId: a second call for a matchId already recorded is a no-op.
    /// </summary>
    void RecordResult(Guid matchId, IReadOnlyList<LeaderboardEntry> awards);

    /// <summary>Every player who has earned at least one point so far this session, ordered by total
    /// points descending (ties broken by player name).</summary>
    IReadOnlyList<LeaderboardEntry> GetStandings();

    /// <summary>Clears all recorded points/standings only. Does not touch ISessionPlayerStore or the
    /// permanent roster.</summary>
    void Reset();
}
