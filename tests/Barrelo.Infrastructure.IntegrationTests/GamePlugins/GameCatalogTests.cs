using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

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

    [Fact]
    public void ReloadClientGames_replaces_the_previous_client_owned_set()
    {
        var catalog = new GameCatalog([]);

        catalog.ReloadClientGames([new FakeGameFactory("old")]);
        catalog.ListAvailable().Select(g => g.GameId).Should().BeEquivalentTo(["old"]);

        catalog.ReloadClientGames([new FakeGameFactory("new")]);

        catalog.ListAvailable().Select(g => g.GameId).Should().BeEquivalentTo(["new"]);
        catalog.Resolve("old").IsError.Should().BeTrue();
    }

    [Fact]
    public void ReloadClientGames_never_shadows_a_built_in_game()
    {
        var catalog = new GameCatalog([new FakeGameFactory("x01")]);

        catalog.ReloadClientGames([new FakeGameFactory("x01"), new FakeGameFactory("yourgame")]);

        catalog.ListAvailable().Select(g => g.GameId).Should().BeEquivalentTo(["x01", "yourgame"]);
        catalog.IsClientOwned("x01").Should().BeFalse();
        catalog.IsClientOwned("yourgame").Should().BeTrue();
    }

    [Fact]
    public void IsClientOwned_is_false_for_a_built_in_game_and_for_an_unknown_one()
    {
        var catalog = new GameCatalog([new FakeGameFactory("x01")]);

        catalog.IsClientOwned("x01").Should().BeFalse();
        catalog.IsClientOwned("unknown").Should().BeFalse();
    }
}
