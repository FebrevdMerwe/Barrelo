using System.Collections.Concurrent;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Entities;

namespace Barrelo.Infrastructure.External.Sessions;

/// <summary>
/// Not persisted/rehydrated across process restart — session-scoped players and bench state are lost on
/// restart (explicit v1 limitation, same as GameSessionManager).
/// </summary>
public sealed class SessionPlayerStore : ISessionPlayerStore
{
    private readonly ConcurrentDictionary<Guid, Player> _sessionPlayers = new();
    private readonly ConcurrentDictionary<Guid, byte> _benchedPermanentIds = new();

    public void AddSessionPlayer(Player player) => _sessionPlayers[player.Id] = player;

    public IReadOnlyList<Player> GetAllSessionPlayers() => _sessionPlayers.Values.ToList();

    public Player? TryGetSessionPlayer(Guid id) => _sessionPlayers.GetValueOrDefault(id);

    public bool RemoveSessionPlayer(Guid id) => _sessionPlayers.TryRemove(id, out _);

    public void BenchPermanentPlayer(Guid id) => _benchedPermanentIds[id] = 0;

    public void UnbenchPermanentPlayer(Guid id) => _benchedPermanentIds.TryRemove(id, out _);

    public bool IsPermanentPlayerBenched(Guid id) => _benchedPermanentIds.ContainsKey(id);

    public IReadOnlySet<Guid> GetBenchedPermanentPlayerIds() => _benchedPermanentIds.Keys.ToHashSet();
}
