using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

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

        await manager.StartSessionAsync(matchId, game, new Dictionary<Guid, int>());
        var found = await manager.TryGetAsync(matchId);

        found.Should().BeSameAs(game);
    }

    [Fact]
    public async Task StartSessionAsync_then_TryGetPlayerGroupsAsync_returns_the_same_mapping()
    {
        var manager = new GameSessionManager();
        var matchId = Guid.NewGuid();
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        var playerGroups = new Dictionary<Guid, int> { [playerA] = 0, [playerB] = 1 };

        await manager.StartSessionAsync(matchId, new NoOpGame(), playerGroups);
        var found = await manager.TryGetPlayerGroupsAsync(matchId);

        found.Should().BeEquivalentTo(playerGroups);
    }

    [Fact]
    public async Task TryGetPlayerGroupsAsync_for_unknown_match_returns_null()
    {
        var manager = new GameSessionManager();

        (await manager.TryGetPlayerGroupsAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task TryGetAsync_for_unknown_match_returns_null()
    {
        var manager = new GameSessionManager();

        (await manager.TryGetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task TryGetActiveMatchIdAsync_initially_returns_null()
    {
        var manager = new GameSessionManager();

        (await manager.TryGetActiveMatchIdAsync()).Should().BeNull();
    }

    [Fact]
    public async Task StartSessionAsync_when_a_session_is_already_active_evicts_it_and_replaces_it()
    {
        var manager = new GameSessionManager();
        var matchA = Guid.NewGuid();
        var matchB = Guid.NewGuid();
        var gameA = new NoOpGame();
        var gameB = new NoOpGame();

        await manager.StartSessionAsync(matchA, gameA, new Dictionary<Guid, int>());
        await manager.StartSessionAsync(matchB, gameB, new Dictionary<Guid, int>());

        (await manager.TryGetActiveMatchIdAsync()).Should().Be(matchB);
        (await manager.TryGetAsync(matchB)).Should().BeSameAs(gameB);
        (await manager.TryGetAsync(matchA)).Should().BeSameAs(gameA); // evicted, not discarded
    }

    [Fact]
    public async Task EndActiveSessionAsync_clears_the_active_slot_so_a_new_match_can_start()
    {
        var manager = new GameSessionManager();
        var matchA = Guid.NewGuid();
        var matchB = Guid.NewGuid();
        await manager.StartSessionAsync(matchA, new NoOpGame(), new Dictionary<Guid, int>());

        await manager.EndActiveSessionAsync(matchA);

        (await manager.TryGetActiveMatchIdAsync()).Should().BeNull();
        await manager.StartSessionAsync(matchB, new NoOpGame(), new Dictionary<Guid, int>());
        (await manager.TryGetActiveMatchIdAsync()).Should().Be(matchB);
    }

    [Fact]
    public async Task EndActiveSessionAsync_for_a_non_active_matchId_is_a_no_op()
    {
        var manager = new GameSessionManager();
        var matchA = Guid.NewGuid();
        await manager.StartSessionAsync(matchA, new NoOpGame(), new Dictionary<Guid, int>());

        await manager.EndActiveSessionAsync(Guid.NewGuid());

        (await manager.TryGetActiveMatchIdAsync()).Should().Be(matchA);
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
