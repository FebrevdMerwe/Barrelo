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
    private readonly ConcurrentDictionary<string, Guid> _boardBindings = new();

    public Task StartSessionAsync(Guid matchId, IGame game)
    {
        _sessions[matchId] = game;
        return Task.CompletedTask;
    }

    public Task<IGame?> TryGetAsync(Guid matchId) =>
        Task.FromResult(_sessions.GetValueOrDefault(matchId));

    public async Task<IAsyncDisposable> LockAsync(Guid matchId, CancellationToken ct)
    {
        var semaphore = _locks.GetOrAdd(matchId, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(ct);
        return new Releaser(semaphore);
    }

    public void BindBoard(string boardId, Guid matchId) => _boardBindings[boardId] = matchId;

    public Guid? ResolveMatchForBoard(string boardId) =>
        _boardBindings.TryGetValue(boardId, out var matchId) ? matchId : null;

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
