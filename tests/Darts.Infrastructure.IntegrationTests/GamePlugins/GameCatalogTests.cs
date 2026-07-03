using Darts.GameSdk;
using Darts.Infrastructure.External.GamePlugins;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.GamePlugins;

public class GameCatalogTests
{
    private sealed class FakeGameFactory(string gameId) : IGameFactory
    {
        public GameDescriptor Describe() => new(gameId, gameId, "test", []);

        public Task<IGame> Create(GameSetup setup, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact]
    public void ListAvailable_returns_descriptors_for_every_registered_factory()
    {
        var catalog = new GameCatalog([new FakeGameFactory("a"), new FakeGameFactory("b")]);

        var games = catalog.ListAvailable();

        games.Select(g => g.GameId).Should().BeEquivalentTo(["a", "b"]);
    }

    [Fact]
    public void Resolve_returns_the_matching_factory()
    {
        var catalog = new GameCatalog([new FakeGameFactory("a")]);

        var result = catalog.Resolve("a");

        result.IsError.Should().BeFalse();
        result.Value.Describe().GameId.Should().Be("a");
    }

    [Fact]
    public void Resolve_with_unknown_game_id_returns_not_found()
    {
        var catalog = new GameCatalog([]);

        var result = catalog.Resolve("unknown");

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.NotFound");
    }
}
