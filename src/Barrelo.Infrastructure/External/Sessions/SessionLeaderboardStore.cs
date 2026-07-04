using System.Collections.Concurrent;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Leaderboard;

namespace Barrelo.Infrastructure.External.Sessions;

/// <summary>
/// Not persisted/rehydrated across process restart — same v1 limitation as GameSessionManager and
/// SessionPlayerStore.
/// </summary>
public sealed class SessionLeaderboardStore : ISessionLeaderboardStore
{
    private readonly ConcurrentDictionary<Guid, (string Name, int Points)> _totals = new();
    private readonly ConcurrentDictionary<Guid, byte> _recordedMatchIds = new();

    public void RecordResult(Guid matchId, IReadOnlyList<LeaderboardEntry> awards)
    {
        if (!_recordedMatchIds.TryAdd(matchId, 0))
            return;

        foreach (var award in awards)
        {
            _totals.AddOrUpdate(
                award.PlayerId,
                _ => (award.PlayerName, award.Points),
                (_, existing) => (award.PlayerName, existing.Points + award.Points));
        }
    }

    public IReadOnlyList<LeaderboardEntry> GetStandings() =>
        _totals
            .Select(kv => new LeaderboardEntry(kv.Key, kv.Value.Name, kv.Value.Points))
            .OrderByDescending(e => e.Points)
            .ThenBy(e => e.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public void Reset()
    {
        _totals.Clear();
        _recordedMatchIds.Clear();
    }
}
