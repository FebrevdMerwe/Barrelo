using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Kickoff.UnitTests;

public class KickoffGameUndoTests
{
    [Fact]
    public async Task Undo_mid_visit_reverts_the_last_kick()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(KickoffTestGame.NorthKick(), CancellationToken.None);
        await game.UndoLastThrow(CancellationToken.None);

        var payload = await game.Payload();
        payload.Ball.X.Should().Be(0.5);
        payload.Ball.Y.Should().Be(0.5);
        payload.CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task Undo_of_a_goal_reverts_the_score_and_restores_turn_ownership()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // side0 scores, side1 now up
        (await game.GetState()).CurrentPlayerId.Should().Be(side1);

        await game.UndoLastThrow(CancellationToken.None);

        var payload = await game.Payload();
        payload.GroupFor(side0).Goals.Should().Be(0);
        payload.LastEvent.Should().BeNull();
        (await game.GetState()).CurrentPlayerId.Should().Be(side0); // turn ownership reverts, not advanced
    }

    [Fact]
    public async Task Undo_of_the_third_kick_in_a_visit_restores_turn_ownership()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var game = await KickoffTestGame.Create([side0, side1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None); // 3rd kick auto-advances

        (await game.GetState()).CurrentPlayerId.Should().Be(side1);

        await game.UndoLastThrow(CancellationToken.None); // undo the 3rd kick

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(side0);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Undo_of_the_match_winning_kick_uncompletes_the_match()
    {
        var side0 = Guid.NewGuid();
        var side1 = Guid.NewGuid();
        var options = new Dictionary<string, string> { ["goalsToWinLeg"] = "1", ["legsToWinMatch"] = "1" };
        var game = await KickoffTestGame.Create([side0, side1], options: options);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None);
        (await game.GetState()).IsComplete.Should().BeTrue();

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();
        state.WinnerPlayerIds.Should().BeNull();
        (await game.Payload()).GroupFor(side0).LegsWon.Should().Be(0);
    }

    [Fact]
    public async Task Undo_with_no_throws_throws_rule_violation()
    {
        var game = await KickoffTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var act = () => game.UndoLastThrow(CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
