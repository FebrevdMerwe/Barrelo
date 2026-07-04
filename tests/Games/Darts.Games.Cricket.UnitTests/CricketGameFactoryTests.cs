using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.Cricket.UnitTests;

public class CricketGameFactoryTests
{
    [Fact]
    public void Describe_returns_stable_game_id_and_display_name()
    {
        var descriptor = new CricketGameFactory().Describe();

        descriptor.GameId.Should().Be("cricket");
        descriptor.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Describe_declares_only_a_player_group_setting()
    {
        var descriptor = new CricketGameFactory().Describe();

        descriptor.Settings.OfType<GameModeSetting>().Should().BeEmpty();

        var playerGroup = descriptor.Settings.OfType<PlayerGroupSetting>().Single();
        playerGroup.MaxGroups.Should().Be(4);
        playerGroup.MaxPlayersPerGroup.Should().Be(4);
    }

    [Fact]
    public async Task Create_throws_when_no_players_supplied()
    {
        var factory = new CricketGameFactory();
        var setup = new GameSetup([], new Dictionary<string, string>());

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Fresh_game_has_zero_marks_and_points_for_every_group()
    {
        var game = await CricketTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var payload = await game.Payload();

        payload.Groups.Should().OnlyContain(g => g.Points == 0 && g.ClosedCount == 0 && g.Marks.All(m => m == 0));
    }
}
