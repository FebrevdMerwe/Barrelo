using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameFactoryTests
{
    [Fact]
    public void Describe_returns_stable_game_id_and_display_name()
    {
        var descriptor = new AroundTheClockGameFactory().Describe();

        descriptor.GameId.Should().Be("around-the-clock");
        descriptor.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Describe_declares_only_a_player_group_setting()
    {
        var descriptor = new AroundTheClockGameFactory().Describe();

        descriptor.Settings.OfType<GameModeSetting>().Should().BeEmpty();

        var playerGroup = descriptor.Settings.OfType<PlayerGroupSetting>().Single();
        playerGroup.MaxGroups.Should().Be(6);
        playerGroup.MaxPlayersPerGroup.Should().Be(4);
    }

    [Fact]
    public void Describe_allows_a_solo_practice_match()
    {
        var descriptor = new AroundTheClockGameFactory().Describe();

        descriptor.MinPlayers.Should().Be(1);
        descriptor.Settings.OfType<PlayerGroupSetting>().Single().MinGroups.Should().Be(1);
    }

    [Fact]
    public async Task Create_throws_when_no_players_supplied()
    {
        var factory = new AroundTheClockGameFactory();
        var setup = new GameSetup([], new Dictionary<string, string>());

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Fresh_game_starts_every_team_on_one_in_round_one()
    {
        var game = await AroundTheClockTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var payload = await game.Payload();

        payload.Round.Should().Be(1);
        payload.TotalSteps.Should().Be(21);
        payload.Groups.Should().OnlyContain(g =>
            g.Progress == 0 && g.TargetNumber == 1 && !g.TargetIsBull && !g.IsFinished);
    }

    [Fact]
    public async Task Fresh_game_reports_leg_and_set_one_since_the_clock_has_neither()
    {
        var game = await AroundTheClockTestGame.Create([Guid.NewGuid()]);

        var state = await game.GetState();

        state.GameId.Should().Be("around-the-clock");
        state.LegNumber.Should().Be(1);
        state.SetNumber.Should().Be(1);
        state.Status.Should().Be(GameStatus.InProgress);
    }
}
