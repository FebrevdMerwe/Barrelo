using Darts.GameSdk;
using Darts.Infrastructure.External.GamePlugins;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.GamePlugins;

public class GameSessionManagerTests
{
    private sealed class NoOpGame : IGame
    {
        public bool IsComplete => false;

        public Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct) => Task.CompletedTask;

        public Task ReceiveEndOfTurn(CancellationToken ct) => Task.CompletedTask;

        public Task UndoLastThrow(CancellationToken ct) => Task.CompletedTask;

        public Task<GameStateSnapshot> GetState() => throw new NotSupportedException();

        public Task<GameResult> GetResult() => throw new NotSupportedException();
    }

    [Fact]
    public async Task StartSessionAsync_then_TryGetAsync_returns_the_same_game_instance()
    {
        var manager = new GameSessionManager();
        var matchId = Guid.NewGuid();
        var game = new NoOpGame();

        await manager.StartSessionAsync(matchId, game);
        var found = await manager.TryGetAsync(matchId);

        found.Should().BeSameAs(game);
    }

    [Fact]
    public async Task TryGetAsync_for_unknown_match_returns_null()
    {
        var manager = new GameSessionManager();

        (await manager.TryGetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public void BindBoard_then_ResolveMatchForBoard_returns_the_bound_match()
    {
        var manager = new GameSessionManager();
        var matchId = Guid.NewGuid();

        manager.BindBoard("board-1", matchId);

        manager.ResolveMatchForBoard("board-1").Should().Be(matchId);
    }

    [Fact]
    public void ResolveMatchForBoard_for_unbound_board_returns_null()
    {
        var manager = new GameSessionManager();

        manager.ResolveMatchForBoard("unbound").Should().BeNull();
    }

    [Fact]
    public async Task LockAsync_serializes_concurrent_access_to_the_same_match()
    {
        var manager = new GameSessionManager();
        var matchId = Guid.NewGuid();
        var concurrentCount = 0;
        var maxObservedConcurrency = 0;
        var gate = new object();

        async Task CriticalSection()
        {
            await using var _ = await manager.LockAsync(matchId, CancellationToken.None);
            lock (gate)
            {
                concurrentCount++;
                maxObservedConcurrency = Math.Max(maxObservedConcurrency, concurrentCount);
            }
            await Task.Delay(20);
            lock (gate)
            {
                concurrentCount--;
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => CriticalSection()));

        maxObservedConcurrency.Should().Be(1);
    }
}
