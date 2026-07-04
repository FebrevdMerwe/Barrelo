using Darts.Application.Commands.Players.DeletePlayer;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using FluentAssertions;
using Moq;
using Player = Darts.Domain.Entities.Player;

namespace Darts.Application.UnitTests.Commands.Players.DeletePlayer;

public class DeletePlayerCommandHandlerTests
{
    private readonly Mock<IPlayerRepository> _playerRepository = new();
    private readonly Mock<ISessionPlayerStore> _sessionPlayerStore = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private DeletePlayerCommandHandler CreateHandler() => new(
        _playerRepository.Object,
        _sessionPlayerStore.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task Unknown_player_id_returns_not_found()
    {
        var playerId = Guid.NewGuid();
        _playerRepository.Setup(r => r.GetById(playerId, It.IsAny<CancellationToken>())).ReturnsAsync((Player?)null);
        var command = new DeletePlayerCommand(playerId);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Player.NotFound");
        _playerRepository.Verify(r => r.Remove(It.IsAny<Player>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Happy_path_removes_the_player_unconditionally_and_saves()
    {
        var playerId = Guid.NewGuid();
        var player = Player.Create("Deshawn Ruiz").Value;
        _playerRepository.Setup(r => r.GetById(playerId, It.IsAny<CancellationToken>())).ReturnsAsync(player);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var command = new DeletePlayerCommand(playerId);

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _playerRepository.Verify(r => r.Remove(player), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _sessionPlayerStore.Verify(s => s.UnbenchPermanentPlayer(playerId), Times.Once);
    }
}
