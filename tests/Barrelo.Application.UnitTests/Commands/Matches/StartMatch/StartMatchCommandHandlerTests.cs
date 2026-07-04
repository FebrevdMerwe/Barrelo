using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;
using FluentAssertions;
using Moq;
using Player = Barrelo.Domain.Entities.Player;

namespace Barrelo.Application.UnitTests.Commands.Matches.StartMatch;

public class StartMatchCommandHandlerTests
{
    private readonly Mock<IGameCatalog> _catalog = new();
    private readonly Mock<IPlayerRepository> _playerRepository = new();
    private readonly Mock<ISessionPlayerStore> _sessionPlayerStore = new();
    private readonly Mock<IGameSessionManager> _sessionManager = new();
    private readonly Mock<IGameFactory> _factory = new();
    private readonly Mock<IGame> _game = new();

    private StartMatchCommandHandler CreateHandler() => new(
        _catalog.Object,
        _playerRepository.Object,
        _sessionPlayerStore.Object,
        _sessionManager.Object,
        new StartMatchCommandValidator(_catalog.Object));

    private void SetUpHappyPath(IReadOnlyList<Guid> playerIds)
    {
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor("x01", "x01", "test", []));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerIds.Select(id => Player.Restore(id, "P", DateTimeOffset.UtcNow)).ToList());
        _sessionPlayerStore.Setup(s => s.GetAllSessionPlayers()).Returns([]);
        _factory.Setup(f => f.Create(It.IsAny<GameSetup>(), It.IsAny<CancellationToken>())).ReturnsAsync(_game.Object);
        _game.Setup(g => g.GetState()).ReturnsAsync(new GameStateSnapshot(
            Guid.Empty, "x01", GameStatus.InProgress, playerIds[0], 1, 1, [], false, null, null));
        _sessionManager
            .Setup(s => s.StartSessionAsync(It.IsAny<Guid>(), It.IsAny<IGame>(), It.IsAny<IReadOnlyDictionary<Guid, int>>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Happy_path_starts_the_session()
    {
        var playerIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.InitialState.MatchId.Should().Be(result.Value.MatchId);
        _sessionManager.Verify(s => s.StartSessionAsync(
            result.Value.MatchId, _game.Object, It.IsAny<IReadOnlyDictionary<Guid, int>>()), Times.Once);
    }

    [Fact]
    public async Task Starting_a_match_while_one_is_already_active_evicts_it_and_starts_the_new_one()
    {
        var playerIds = new List<Guid> { Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        _sessionManager.Setup(s => s.TryGetActiveMatchIdAsync()).ReturnsAsync(Guid.NewGuid());
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sessionManager.Verify(s => s.StartSessionAsync(
            result.Value.MatchId, _game.Object, It.IsAny<IReadOnlyDictionary<Guid, int>>()), Times.Once);
    }

    [Fact]
    public async Task Unknown_game_id_returns_catalog_error_without_touching_repositories()
    {
        _catalog.Setup(c => c.Resolve("unknown")).Returns(Error.NotFound("Game.NotFound", "nope"));
        var command = new StartMatchCommand("unknown", [Guid.NewGuid()], new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _sessionManager.Verify(s => s.StartSessionAsync(
            It.IsAny<Guid>(), It.IsAny<IGame>(), It.IsAny<IReadOnlyDictionary<Guid, int>>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_player_id_returns_error()
    {
        var playerIds = new List<Guid> { Guid.NewGuid() };
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor("x01", "x01", "test", []));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _sessionPlayerStore.Setup(s => s.GetAllSessionPlayers()).Returns([]);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.PlayersNotFound");
    }

    [Fact]
    public async Task Session_scoped_player_id_is_accepted_as_a_valid_participant()
    {
        var permanentId = Guid.NewGuid();
        var sessionPlayer = Player.Create("Session Sam").Value;
        var playerIds = new List<Guid> { permanentId, sessionPlayer.Id };
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor("x01", "x01", "test", []));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Player.Restore(permanentId, "Permanent Pat", DateTimeOffset.UtcNow)]);
        _sessionPlayerStore.Setup(s => s.GetAllSessionPlayers()).Returns([sessionPlayer]);
        _factory.Setup(f => f.Create(It.IsAny<GameSetup>(), It.IsAny<CancellationToken>())).ReturnsAsync(_game.Object);
        _game.Setup(g => g.GetState()).ReturnsAsync(new GameStateSnapshot(
            Guid.Empty, "x01", GameStatus.InProgress, playerIds[0], 1, 1, [], false, null, null));
        _sessionManager
            .Setup(s => s.StartSessionAsync(It.IsAny<Guid>(), It.IsAny<IGame>(), It.IsAny<IReadOnlyDictionary<Guid, int>>()))
            .Returns(Task.CompletedTask);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Grouped_happy_path_passes_group_indexes_to_the_plugin()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var playerIds = new List<Guid> { a1, b1 };
        SetUpHappyPath(playerIds);
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor(
            "x01", "x01", "test", [new PlayerGroupSetting("teams", "Teams", MaxGroups: 2, MaxPlayersPerGroup: 4)]));
        var playerGroups = new Dictionary<Guid, int> { [a1] = 0, [b1] = 1 };
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>(), playerGroups);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _factory.Verify(f => f.Create(
            It.Is<GameSetup>(s => s.PlayerGroups![a1] == 0 && s.PlayerGroups![b1] == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Group_declaring_game_requires_every_player_to_be_assigned()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var playerIds = new List<Guid> { a1, b1 };
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor(
            "x01", "x01", "test", [new PlayerGroupSetting("teams", "Teams", MaxGroups: 2, MaxPlayersPerGroup: 4)]));
        var command = new StartMatchCommand(
            "x01", playerIds, new Dictionary<string, string>(), new Dictionary<Guid, int> { [a1] = 0 });

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _sessionManager.Verify(s => s.StartSessionAsync(
            It.IsAny<Guid>(), It.IsAny<IGame>(), It.IsAny<IReadOnlyDictionary<Guid, int>>()), Times.Never);
    }

    [Fact]
    public async Task Empty_player_list_fails_validation_before_touching_the_catalog()
    {
        var command = new StartMatchCommand("x01", [], new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _catalog.Verify(c => c.Resolve(It.IsAny<string>()), Times.Never);
    }
}
