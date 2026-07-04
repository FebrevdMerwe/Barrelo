using Barrelo.Application.Commands.Leaderboard.ResetLeaderboard;
using Barrelo.Application.Common.Interfaces.Services;
using FluentAssertions;
using Moq;

namespace Barrelo.Application.UnitTests.Commands.Leaderboard.ResetLeaderboard;

public class ResetLeaderboardCommandHandlerTests
{
    private readonly Mock<ISessionLeaderboardStore> _leaderboardStore = new();

    private ResetLeaderboardCommandHandler CreateHandler() => new(_leaderboardStore.Object);

    [Fact]
    public async Task Handle_resets_the_store_and_returns_success()
    {
        var result = await CreateHandler().Handle(new ResetLeaderboardCommand(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _leaderboardStore.Verify(s => s.Reset(), Times.Once);
    }
}
