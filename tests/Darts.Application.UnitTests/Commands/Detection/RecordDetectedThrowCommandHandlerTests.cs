using Darts.Application.Commands.Detection.RecordDetectedThrow;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Leaderboard;
using Darts.Application.Common.Notifications;
using Darts.Application.Queries.Players.ListPlayers;
using Darts.GameSdk;
using FluentAssertions;
using Moq;

namespace Darts.Application.UnitTests.Commands.Detection;

public class RecordDetectedThrowCommandHandlerTests
{
    private readonly Mock<IGameSessionManager> _sessionManager = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<ISessionLeaderboardStore> _leaderboardStore = new();
    private readonly Mock<IGame> _game = new();
    private readonly Guid _matchId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();

    public RecordDetectedThrowCommandHandlerTests()
    {
        _leaderboardStore.Setup(s => s.GetStandings()).Returns([]);
    }

    private RecordDetectedThrowCommandHandler CreateHandler()
    {
        var executor = new GameCommandExecutor(_sessionManager.Object, _dispatcher.Object, _leaderboardStore.Object);
        return new RecordDetectedThrowCommandHandler(executor, new RecordDetectedThrowCommandValidator());
    }

    private void ActivateMatch()
    {
        _sessionManager.Setup(s => s.TryGetActiveMatchIdAsync()).ReturnsAsync(_matchId);
        _sessionManager.Setup(s => s.LockAsync(_matchId, It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        _sessionManager.Setup(s => s.TryGetAsync(_matchId)).ReturnsAsync(_game.Object);
    }

    private GameStateSnapshot Snapshot(bool isComplete = false) =>
        new(Guid.Empty, "x01", isComplete ? GameStatus.Complete : GameStatus.InProgress, _playerId, 1, 1, [], isComplete, null, null);

    [Fact]
    public async Task Valid_throw_records_the_throw_and_publishes_the_updated_state()
    {
        ActivateMatch();
        _game.Setup(g => g.GetState()).ReturnsAsync(Snapshot());
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MatchId.Should().Be(_matchId);
        _game.Verify(g => g.ReceiveThrow(It.Is<DetectedThrow>(t => t.Segment == 20 && t.Ring == Ring.Triple && t.Score == 60), It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Publish(It.Is<GameStateChangedEvent>(e => e.MatchId == _matchId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rule_violation_from_the_plugin_maps_to_a_validation_error_and_does_not_publish()
    {
        ActivateMatch();
        _game.Setup(g => g.GetState()).ReturnsAsync(Snapshot());
        _game.Setup(g => g.ReceiveThrow(It.IsAny<DetectedThrow>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GameRuleViolationException("game already finished"));
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.RuleViolation");
        _dispatcher.Verify(d => d.Publish(It.IsAny<GameStateChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task No_active_match_returns_error_without_locking_or_touching_the_game()
    {
        _sessionManager.Setup(s => s.TryGetActiveMatchIdAsync()).ReturnsAsync((Guid?)null);
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Match.NoActiveMatch");
        _sessionManager.Verify(s => s.LockAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Invalid_segment_fails_validation_before_checking_for_an_active_match()
    {
        var command = new RecordDetectedThrowCommand(25, Ring.Inner);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        _sessionManager.Verify(s => s.TryGetActiveMatchIdAsync(), Times.Never);
    }

    [Fact]
    public async Task Completing_a_match_records_points_and_attaches_standings_to_the_response()
    {
        var runnerUpId = Guid.NewGuid();
        ActivateMatch();
        _game.Setup(g => g.GetState()).ReturnsAsync(Snapshot(isComplete: true));
        _game.Setup(g => g.GetResult()).ReturnsAsync(new GameResult([_playerId], [_playerId, runnerUpId]));
        _sessionManager
            .Setup(s => s.TryGetPlayerGroupsAsync(_matchId))
            .ReturnsAsync(new Dictionary<Guid, int> { [_playerId] = 0, [runnerUpId] = 1 });
        _dispatcher
            .Setup(d => d.Send(It.IsAny<ListPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<PlayerListItem>)
            [
                new PlayerListItem(_playerId, "Winner", DateTimeOffset.UtcNow, true, false),
                new PlayerListItem(runnerUpId, "Runner Up", DateTimeOffset.UtcNow, true, false),
            ]);
        var standings = new List<LeaderboardEntry>
        {
            new(_playerId, "Winner", 3),
            new(runnerUpId, "Runner Up", 2),
        };
        _leaderboardStore.Setup(s => s.GetStandings()).Returns(standings);
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.SessionLeaderboard.Should().BeEquivalentTo(standings);
        _leaderboardStore.Verify(s => s.RecordResult(_matchId, It.Is<IReadOnlyList<LeaderboardEntry>>(awards =>
            awards.Count == 2 &&
            awards.Any(a => a.PlayerId == _playerId && a.Points == 3) &&
            awards.Any(a => a.PlayerId == runnerUpId && a.Points == 2))), Times.Once);
        _sessionManager.Verify(s => s.EndActiveSessionAsync(_matchId), Times.Once);
    }
}
