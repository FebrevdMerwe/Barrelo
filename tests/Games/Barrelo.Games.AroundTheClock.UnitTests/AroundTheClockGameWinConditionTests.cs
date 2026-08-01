using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameWinConditionTests
{
    [Fact]
    public async Task The_first_inner_bull_ends_the_match_on_the_spot()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ClimbToBull(p1);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.Status.Should().Be(GameStatus.Complete);
        state.WinnerPlayerIds.Should().Equal(p1);
        state.CurrentPlayerId.Should().BeNull();
    }

    [Fact]
    public async Task Throwing_after_the_match_is_over_throws_rule_violation()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(AroundTheClockTestGame.BullStep);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        var act = () => game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task GetResult_before_the_match_is_over_throws_rule_violation()
    {
        var game = await AroundTheClockTestGame.Create([Guid.NewGuid()]);

        var act = () => game.GetResult();

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Final_standings_put_the_winner_first_then_the_field_by_progress()
    {
        var winner = Guid.NewGuid();
        var chaser = Guid.NewGuid();
        var laggard = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([winner, chaser, laggard]);

        // Give the chaser a rung before the winner runs away with it, so the two trailing teams are
        // separated by progress rather than by roster order.
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> chaser
        await game.ReceiveThrow(TestThrow.Of(Ring.OuterSingle, 1), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> laggard
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> winner

        await game.ClimbToBull(winner);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        var result = await game.GetResult();

        result.WinnerPlayerIds.Should().Equal(winner);
        result.FinalStandings.Should().Equal(winner, chaser, laggard);
    }

    [Fact]
    public async Task A_team_win_lists_every_member_in_the_standings_ahead_of_the_field()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await AroundTheClockTestGame.Create([a1, a2, b1], groups);

        await game.ClimbToBull(a1);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        var result = await game.GetResult();

        result.FinalStandings.Should().Equal(a1, a2, b1);
    }
}
