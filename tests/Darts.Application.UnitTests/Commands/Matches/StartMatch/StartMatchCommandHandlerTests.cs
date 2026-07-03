using Darts.Application.Commands.Matches.StartMatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;
using ErrorOr;
using FluentAssertions;
using Moq;
using DomainMatch = Darts.Domain.Entities.Match;
using Player = Darts.Domain.Entities.Player;

namespace Darts.Application.UnitTests.Commands.Matches.StartMatch;

public class StartMatchCommandHandlerTests
{
    private readonly Mock<IGameCatalog> _catalog = new();
    private readonly Mock<IPlayerRepository> _playerRepository = new();
    private readonly Mock<IMatchRepository> _matchRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IGameSessionManager> _sessionManager = new();
    private readonly Mock<IGameFactory> _factory = new();
    private readonly Mock<IGame> _game = new();

    private StartMatchCommandHandler CreateHandler() => new(
        _catalog.Object,
        _playerRepository.Object,
        _matchRepository.Object,
        _unitOfWork.Object,
        _sessionManager.Object,
        new StartMatchCommandValidator(_catalog.Object));

    private void SetUpHappyPath(IReadOnlyList<Guid> playerIds)
    {
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _factory.Setup(f => f.Describe()).Returns(new GameDescriptor("x01", "x01", "test", []));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerIds.Select(id => Player.Create("P").Value).ToList());
        _factory.Setup(f => f.Create(It.IsAny<GameSetup>(), It.IsAny<CancellationToken>())).ReturnsAsync(_game.Object);
        _game.Setup(g => g.GetState()).ReturnsAsync(new GameStateSnapshot(
            Guid.Empty, "x01", GameStatus.InProgress, playerIds[0], 1, 1, [], false, null, null));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _sessionManager.Setup(s => s.TryStartSessionAsync(It.IsAny<Guid>(), It.IsAny<IGame>())).ReturnsAsync(true);
    }

    [Fact]
    public async Task Happy_path_creates_match_and_starts_the_session()
    {
        var playerIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.InitialState.MatchId.Should().Be(result.Value.MatchId);
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionManager.Verify(s => s.TryStartSessionAsync(result.Value.MatchId, _game.Object), Times.Once);
    }

    [Fact]
    public async Task Match_already_active_returns_conflict_without_creating_a_new_match()
    {
        var playerIds = new List<Guid> { Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        _sessionManager.Setup(s => s.TryGetActiveMatchIdAsync()).ReturnsAsync(Guid.NewGuid());
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.AlreadyActive");
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Losing_the_session_start_race_returns_conflict_without_persisting_the_match()
    {
        var playerIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        _sessionManager.Setup(s => s.TryStartSessionAsync(It.IsAny<Guid>(), It.IsAny<IGame>())).ReturnsAsync(false);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.AlreadyActive");
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_game_id_returns_catalog_error_without_touching_repositories()
    {
        _catalog.Setup(c => c.Resolve("unknown")).Returns(Error.NotFound("Game.NotFound", "nope"));
        var command = new StartMatchCommand("unknown", [Guid.NewGuid()], new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Never);
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
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>());

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.PlayersNotFound");
    }

    [Fact]
    public async Task Grouped_happy_path_persists_group_indexes_and_passes_them_to_the_plugin()
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
        _matchRepository.Verify(r => r.Add(
            It.Is<DomainMatch>(m => m.Participants[0].GroupIndex == 0 && m.Participants[1].GroupIndex == 1),
            It.IsAny<CancellationToken>()), Times.Once);
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
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Never);
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
