using System.Collections.Concurrent;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;

namespace Darts.Infrastructure.External.GamePlugins;

/// <summary>
/// Not persisted/rehydrated across process restart — an interrupted match is lost (explicit v1 limitation).
/// Starting a session always evicts whatever was previously active.
/// </summary>
public sealed class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<Guid, IGame> _sessions = new();
    private readonly ConcurrentDictionary<Guid, IReadOnlyDictionary<Guid, int>> _playerGroupsByMatch = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _locks = new();
    private readonly object _activeMatchGate = new();
    private Guid? _activeMatchId;

    public Task<Guid?> TryGetActiveMatchIdAsync()
    {
        lock (_activeMatchGate)
        {
            return Task.FromResult(_activeMatchId);
        }
    }

    public Task StartSessionAsync(Guid matchId, IGame game, IReadOnlyDictionary<Guid, int> playerGroups)
    {
        lock (_activeMatchGate)
        {
            _activeMatchId = matchId;
        }

        _sessions[matchId] = game;
        _playerGroupsByMatch[matchId] = playerGroups;
        return Task.CompletedTask;
    }

    public Task<IGame?> TryGetAsync(Guid matchId) =>
        Task.FromResult(_sessions.GetValueOrDefault(matchId));

    public Task<IReadOnlyDictionary<Guid, int>?> TryGetPlayerGroupsAsync(Guid matchId) =>
        Task.FromResult(_playerGroupsByMatch.GetValueOrDefault(matchId));

    public async Task<IAsyncDisposable> LockAsync(Guid matchId, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd(matchId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    public Task EndActiveSessionAsync(Guid matchId)
    {
        lock (_activeMatchGate)
        {
            if (_activeMatchId == matchId)
                _activeMatchId = null;
        }

        return Task.CompletedTask;
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
