using Darts.Application.Commands.Matches.StartMatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Domain.Enums;
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
        new StartMatchCommandValidator());

    private void SetUpHappyPath(IReadOnlyList<Guid> playerIds)
    {
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(playerIds.Select(id => Player.Create("P").Value).ToList());
        _factory.Setup(f => f.Create(It.IsAny<GameSetup>(), It.IsAny<CancellationToken>())).ReturnsAsync(_game.Object);
        _game.Setup(g => g.GetState()).ReturnsAsync(new GameStateSnapshot(
            Guid.Empty, "x01", GameStatus.InProgress, playerIds[0], 1, 1, [], false, null, null));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task Happy_path_creates_match_starts_session_and_binds_manual_board()
    {
        var playerIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        SetUpHappyPath(playerIds);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>(), InputSource.Manual);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.InitialState.MatchId.Should().Be(result.Value.MatchId);
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionManager.Verify(s => s.StartSessionAsync(result.Value.MatchId, _game.Object), Times.Once);
        _sessionManager.Verify(s => s.BindBoard("manual", result.Value.MatchId), Times.Once);
    }

    [Fact]
    public async Task Unknown_game_id_returns_catalog_error_without_touching_repositories()
    {
        _catalog.Setup(c => c.Resolve("unknown")).Returns(Error.NotFound("Game.NotFound", "nope"));
        var command = new StartMatchCommand("unknown", [Guid.NewGuid()], new Dictionary<string, string>(), InputSource.Manual);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _matchRepository.Verify(r => r.Add(It.IsAny<DomainMatch>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unknown_player_id_returns_error()
    {
        var playerIds = new List<Guid> { Guid.NewGuid() };
        _catalog.Setup(c => c.Resolve("x01")).Returns(ErrorOrFactory.From(_factory.Object));
        _playerRepository
            .Setup(r => r.GetByIds(playerIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var command = new StartMatchCommand("x01", playerIds, new Dictionary<string, string>(), InputSource.Manual);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.PlayersNotFound");
    }

    [Fact]
    public async Task Empty_player_list_fails_validation_before_touching_the_catalog()
    {
        var command = new StartMatchCommand("x01", [], new Dictionary<string, string>(), InputSource.Manual);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _catalog.Verify(c => c.Resolve(It.IsAny<string>()), Times.Never);
    }
}
