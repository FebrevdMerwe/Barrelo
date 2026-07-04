using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Kickoff.UnitTests;

public class KickoffGameWinConditionTests
{
    [Fact]
    public async Task Reaching_goals_to_win_leg_awards_a_leg_and_resets_goals_without_completing_the_match()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var options = new Dictionary<string, string> { ["goalsToWinLeg"] = "1", ["legsToWinMatch"] = "2" };
        var game = await KickoffTestGame.Create([side0, side1], options: options);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // side0 wins leg 1

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();

        var payload = await game.Payload();
        payload.GroupFor(side0).LegsWon.Should().Be(1);
        payload.GroupFor(side0).Goals.Should().Be(0); // reset for the next leg
        payload.GroupFor(side1).Goals.Should().Be(0);
    }

    [Fact]
    public async Task Reaching_legs_to_win_match_completes_the_game()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var options = new Dictionary<string, string> { ["goalsToWinLeg"] = "1", ["legsToWinMatch"] = "2" };
        var game = await KickoffTestGame.Create([side0, side1], options: options);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // leg 1 to side0
        await game.ReceiveEndOfTurn(CancellationToken.None); // side1's turn passes without scoring, back to side0
        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // leg 2 to side0 -> match won

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([side0]);
    }

    [Fact]
    public async Task Default_options_require_three_goals_per_leg_and_two_legs_to_win()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        for (var i = 0; i < 2; i++)
            await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None);

        (await game.Payload()).GroupFor(side0).Goals.Should().Be(2);
        (await game.GetState()).IsComplete.Should().BeFalse();

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // 3rd goal -> leg won

        var payload = await game.Payload();
        payload.GroupFor(side0).LegsWon.Should().Be(1);
        payload.GroupFor(side0).Goals.Should().Be(0);
        (await game.GetState()).IsComplete.Should().BeFalse(); // only 1 of 2 legs so far
    }

    [Fact]
    public async Task GetResult_before_completion_throws()
    {
        var game = await KickoffTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var act = () => game.GetResult();

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task GetResult_ranks_the_losing_side_by_legs_then_goals()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var options = new Dictionary<string, string> { ["goalsToWinLeg"] = "1", ["legsToWinMatch"] = "1" };
        var game = await KickoffTestGame.Create([side0, side1], options: options);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // side0 wins outright

        var result = await game.GetResult();
        result.WinnerPlayerIds.Should().BeEquivalentTo([side0]);
        result.FinalStandings.Should().Equal(side0, side1);
    }
}
