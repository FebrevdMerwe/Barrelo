using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Kickoff.UnitTests;

public class KickoffGameFactoryTests
{
    [Fact]
    public void Describe_returns_stable_game_id_and_display_name()
    {
        var descriptor = new KickoffGameFactory().Describe();

        descriptor.GameId.Should().Be("kickoff");
        descriptor.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Describe_declares_a_two_team_player_group_setting()
    {
        var descriptor = new KickoffGameFactory().Describe();

        descriptor.Settings.OfType<GameModeSetting>().Should().BeEmpty();

        var playerGroup = descriptor.Settings.OfType<PlayerGroupSetting>().Single();
        playerGroup.MaxGroups.Should().Be(2);
        playerGroup.MaxPlayersPerGroup.Should().Be(4);
    }

    [Fact]
    public async Task Create_throws_when_no_players_supplied()
    {
        var factory = new KickoffGameFactory();
        var setup = new GameSetup([], new Dictionary<string, string>());

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Create_throws_when_only_one_team_is_present()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var factory = new KickoffGameFactory();
        // No explicit PlayerGroups -> each player defaults to its own singleton group, i.e. 2 groups here,
        // which is fine; force a single shared group instead to hit the "only one team" rejection.
        var setup = new GameSetup([p1, p2], new Dictionary<string, string>(), new Dictionary<Guid, int> { [p1] = 0, [p2] = 0 });

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Create_throws_when_more_than_two_teams_are_present()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var factory = new KickoffGameFactory();
        var setup = new GameSetup([p1, p2, p3], new Dictionary<string, string>());

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Fresh_two_player_game_starts_at_nil_nil_with_ball_at_center()
    {
        var game = await KickoffTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var payload = await game.Payload();

        payload.Groups.Should().OnlyContain(g => g.Goals == 0 && g.LegsWon == 0);
        payload.Ball.X.Should().Be(0.5);
        payload.Ball.Y.Should().Be(0.5);
    }
}
