using Darts.Application.Commands.Detection.RecordDetectedThrow;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Application.Common.Notifications;
using Darts.Domain.Entities;
using Darts.GameSdk;
using FluentAssertions;
using Moq;

namespace Darts.Application.UnitTests.Commands.Detection;

public class RecordDetectedThrowCommandHandlerTests
{
    private readonly Mock<IGameSessionManager> _sessionManager = new();
    private readonly Mock<IMatchRepository> _matchRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<IGame> _game = new();
    private readonly Guid _matchId = Guid.NewGuid();
    private readonly Guid _playerId = Guid.NewGuid();

    private RecordDetectedThrowCommandHandler CreateHandler()
    {
        var executor = new GameCommandExecutor(_sessionManager.Object, _matchRepository.Object, _unitOfWork.Object, _dispatcher.Object);
        return new RecordDetectedThrowCommandHandler(executor, new RecordDetectedThrowCommandValidator());
    }

    private void ActivateMatch()
    {
        _sessionManager.Setup(s => s.TryGetActiveMatchIdAsync()).ReturnsAsync(_matchId);
        _sessionManager.Setup(s => s.LockAsync(_matchId, It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        _sessionManager.Setup(s => s.TryGetAsync(_matchId)).ReturnsAsync(_game.Object);
        _matchRepository.Setup(r => r.AddThrowRecord(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<Domain.Enums.Ring>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<Domain.Enums.DetectionSource>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ThrowRecord.Create(_matchId, _playerId, 1, 1, 1, 20, Domain.Enums.Ring.Triple, 60, "T20", 0, 1, Domain.Enums.DetectionSource.Manual, DateTimeOffset.UtcNow));
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private GameStateSnapshot Snapshot(bool isComplete = false) =>
        new(Guid.Empty, "x01", isComplete ? GameStatus.Complete : GameStatus.InProgress, _playerId, 1, 1, [], isComplete, null, null);

    [Fact]
    public async Task Valid_throw_records_the_throw_and_publishes_the_updated_state()
    {
        ActivateMatch();
        _game.SetupSequence(g => g.GetState())
            .ReturnsAsync(Snapshot()) // pre-state: captures the throwing player
            .ReturnsAsync(Snapshot()); // post-state: returned to the caller
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.MatchId.Should().Be(_matchId);
        _game.Verify(g => g.ReceiveThrow(It.Is<DetectedThrow>(t => t.Segment == 20 && t.Ring == Ring.Triple && t.Score == 60), It.IsAny<CancellationToken>()), Times.Once);
        var expectedPosition = BoardGeometry.CenterOf(20, Ring.Triple);
        _matchRepository.Verify(r => r.AddThrowRecord(
            _matchId, _playerId, 1, 1, 20, Domain.Enums.Ring.Triple, 60, "T20", expectedPosition.X, expectedPosition.Y, Domain.Enums.DetectionSource.Manual, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Publish(It.Is<GameStateChangedEvent>(e => e.MatchId == _matchId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rule_violation_from_the_plugin_maps_to_a_validation_error_and_does_not_persist()
    {
        ActivateMatch();
        _game.Setup(g => g.GetState()).ReturnsAsync(Snapshot());
        _game.Setup(g => g.ReceiveThrow(It.IsAny<DetectedThrow>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GameRuleViolationException("game already finished"));
        var command = new RecordDetectedThrowCommand(20, Ring.Triple);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.RuleViolation");
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
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
}
