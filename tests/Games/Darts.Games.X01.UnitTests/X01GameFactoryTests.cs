using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.X01.UnitTests;

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
    public async Task Create_with_no_options_uses_default_501_starting_score()
    {
        var factory = new X01GameFactory();
        var setup = new GameSetup([Guid.NewGuid(), Guid.NewGuid()], new Dictionary<string, string>());

        var game = await factory.Create(setup, CancellationToken.None);
        var state = await game.GetState();
        var payload = (X01StatePayload)state.Payload!;

        payload.Players.Should().OnlyContain(p => p.RemainingScore == 501);
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

        payload.Players.Single().RemainingScore.Should().Be(301);
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
