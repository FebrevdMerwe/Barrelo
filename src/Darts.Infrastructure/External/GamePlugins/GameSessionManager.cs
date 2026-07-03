using System.Collections.Concurrent;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;

namespace Darts.Infrastructure.External.GamePlugins;

/// <summary>
/// Not persisted/rehydrated across process restart — an interrupted match is lost (explicit v1 limitation).
/// </summary>
public sealed class GameSessionManager : IGameSessionManager
{
    private readonly ConcurrentDictionary<Guid, IGame> _sessions = new();
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

    public Task<bool> TryStartSessionAsync(Guid matchId, IGame game)
    {
        lock (_activeMatchGate)
        {
            if (_activeMatchId is not null)
                return Task.FromResult(false);

            _activeMatchId = matchId;
        }

        _sessions[matchId] = game;
        return Task.FromResult(true);
    }

    public Task<IGame?> TryGetAsync(Guid matchId) =>
        Task.FromResult(_sessions.GetValueOrDefault(matchId));

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
