using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Kickoff.UnitTests;

public class KickoffGameScoringRulesTests
{
    [Fact]
    public async Task A_miss_does_not_move_the_ball()
    {
        var game = await KickoffTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var payload = await game.Payload();
        payload.Ball.X.Should().Be(0.5);
        payload.Ball.Y.Should().Be(0.5);
        payload.CurrentVisitThrows.Should().HaveCount(1);
    }

    [Fact]
    public async Task A_kick_toward_the_east_goal_scores_for_side_zero()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None);

        var payload = await game.Payload();
        payload.GroupFor(side0).Goals.Should().Be(1);
        payload.GroupFor(side1).Goals.Should().Be(0);
        payload.LastEvent!.Text.Should().Be("GOAL!");
        payload.Ball.X.Should().Be(0.5); // restarted at center
        payload.Ball.Y.Should().Be(0.5);
    }

    [Fact]
    public async Task A_kick_toward_the_west_goal_scores_for_side_one()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]); // side0 (group 0) kicks off

        await game.ReceiveEndOfTurn(CancellationToken.None); // side0's turn passes without scoring
        await game.ReceiveThrow(KickoffTestGame.WestGoalKick(), CancellationToken.None); // side1 scores normally

        var payload = await game.Payload();
        payload.GroupFor(side1).Goals.Should().Be(1);
        payload.LastEvent!.Text.Should().Be("GOAL!");
    }

    [Fact]
    public async Task An_own_goal_credits_the_other_side_and_the_scoring_side_keeps_the_ball()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]); // side0 kicks first

        // Side 0 is on the ball but kicks the ball into the west goal, which credits side 1.
        await game.ReceiveThrow(KickoffTestGame.WestGoalKick(), CancellationToken.None);

        var payload = await game.Payload();
        payload.GroupFor(side1).Goals.Should().Be(1);
        payload.GroupFor(side0).Goals.Should().Be(0);
        payload.LastEvent!.Text.Should().Be("OWN GOAL!");

        // Side 0 put it in their own net, so side 0 is the side that conceded and restarts play.
        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(side0);
    }

    [Fact]
    public async Task Ball_leaving_the_pitch_ends_the_visit_and_hands_possession_to_the_other_side_with_position_retained()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(KickoffTestGame.NorthKick(), CancellationToken.None); // reaches the touchline, still in play
        (await game.GetState()).CurrentPlayerId.Should().Be(side0); // same visit continues

        await game.ReceiveThrow(KickoffTestGame.NorthKick(), CancellationToken.None); // pushes past it -> out

        var payload = await game.Payload();
        payload.LastEvent!.Text.Should().Contain("OUT");
        payload.Ball.Y.Should().Be(0.0); // clamped, position retained (not reset to center)
        payload.CurrentVisitThrows.Should().BeEmpty();

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(side1);
    }

    [Fact]
    public async Task Three_kicks_with_no_goal_or_out_ends_the_visit_and_alternates_sides()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(side0);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None); // 3rd kick auto-advances

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(side1);
        (await game.Payload()).CurrentVisitThrows.Should().BeEmpty();
    }
}
