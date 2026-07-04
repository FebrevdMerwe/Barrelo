using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.X01.UnitTests;

public class X01GameFactoryTests
{
    [Fact]
    public void Describe_returns_stable_game_id_and_display_name()
    {
        var descriptor = new X01GameFactory().Describe();

        descriptor.GameId.Should().Be("x01");
        descriptor.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Describe_declares_a_game_mode_setting_and_a_player_group_setting()
    {
        var descriptor = new X01GameFactory().Describe();

        var gameMode = descriptor.Settings.OfType<GameModeSetting>().Single();
        gameMode.Choices.Select(c => c.Value).Should().BeEquivalentTo("501", "301", "701");
        gameMode.DefaultValue.Should().Be("501");

        var playerGroup = descriptor.Settings.OfType<PlayerGroupSetting>().Single();
        playerGroup.MaxGroups.Should().Be(2);
        playerGroup.MaxPlayersPerGroup.Should().Be(4);
    }

    [Fact]
    public async Task Create_with_no_options_uses_default_501_starting_score()
    {
        var factory = new X01GameFactory();
        var setup = new GameSetup([Guid.NewGuid(), Guid.NewGuid()], new Dictionary<string, string>());

        var game = await factory.Create(setup, CancellationToken.None);
        var state = await game.GetState();
        var payload = (X01StatePayload)state.Payload!;

        payload.Groups.Should().OnlyContain(g => g.RemainingScore == 501);
    }

    [Fact]
    public async Task Create_honors_explicit_options()
    {
        var factory = new X01GameFactory();
        var setup = new GameSetup(
            [Guid.NewGuid()],
            new Dictionary<string, string> { ["startingScore"] = "301" });

        var game = await factory.Create(setup, CancellationToken.None);
        var state = await game.GetState();
        var payload = (X01StatePayload)state.Payload!;

        payload.Groups.Single().RemainingScore.Should().Be(301);
    }

    [Fact]
    public async Task Create_throws_when_no_players_supplied()
    {
        var factory = new X01GameFactory();
        var setup = new GameSetup([], new Dictionary<string, string>());

        var act = () => factory.Create(setup, CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
