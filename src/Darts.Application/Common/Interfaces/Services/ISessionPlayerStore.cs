using Darts.Domain.Entities;

namespace Darts.Application.Common.Interfaces.Services;

/// <summary>
/// Holds session-scoped players (added ad hoc for tonight's darts, never written to the database) and
/// tracks which permanent players are benched for the current session. Not persisted/rehydrated across
/// process restart — a session lasts as long as the process does (explicit v1 limitation, same as
/// IGameSessionManager).
/// </summary>
public interface ISessionPlayerStore
{
    /// <summary>Adds (or re-adds, e.g. to undo an erase) a session-scoped player under its own id.</summary>
    void AddSessionPlayer(Player player);

    /// <summary>All currently known session-scoped players.</summary>
    IReadOnlyList<Player> GetAllSessionPlayers();

    Player? TryGetSessionPlayer(Guid id);

    /// <summary>Fully removes a session-scoped player. Returns false if no such player was known.</summary>
    bool RemoveSessionPlayer(Guid id);

    /// <summary>Hides a permanent player from the chalkboard for the remainder of this session.</summary>
    void BenchPermanentPlayer(Guid id);

    /// <summary>Reverses BenchPermanentPlayer.</summary>
    void UnbenchPermanentPlayer(Guid id);

    bool IsPermanentPlayerBenched(Guid id);

    IReadOnlySet<Guid> GetBenchedPermanentPlayerIds();
}
