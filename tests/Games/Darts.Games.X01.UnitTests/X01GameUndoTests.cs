using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.X01.UnitTests;

public class X01GameUndoTests
{
    [Fact]
    public async Task Undo_mid_visit_reverts_the_last_throw()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "100" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // 501-esque: 100-60=40
        await game.UndoLastThrow(CancellationToken.None);

        var payload = await game.Payload();
        payload.Players.Single().RemainingScore.Should().Be(100);
        payload.CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task Undo_across_a_leg_boundary_reverts_the_checkout()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2], new Dictionary<string, string>
        {
            ["startingScore"] = "40",
            ["legsToWin"] = "2",
            ["setsToWin"] = "1",
        });

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // P1 checks out leg 1
        var beforeUndo = await game.GetState();
        beforeUndo.LegNumber.Should().Be(2);

        await game.UndoLastThrow(CancellationToken.None); // undo the checkout dart itself

        var state = await game.GetState();
        state.LegNumber.Should().Be(1);
        state.CurrentPlayerId.Should().Be(p1);
        state.IsComplete.Should().BeFalse();
        var payload = (X01StatePayload)state.Payload!;
        payload.Players.Single(p => p.PlayerId == p1).RemainingScore.Should().Be(40);
        payload.Players.Single(p => p.PlayerId == p1).LegsWon.Should().Be(0);
    }

    [Fact]
    public async Task Undo_of_a_busting_dart_restores_the_pre_bust_visit_and_turn_ownership()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2], new Dictionary<string, string> { ["startingScore"] = "25" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Inner, 20), CancellationToken.None); // 25-20=5
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // busts: 5-60<0, reverts to 25, turn passes to P2

        var busted = await game.GetState();
        busted.CurrentPlayerId.Should().Be(p2);
        ((X01StatePayload)busted.Payload!).Players.Single(p => p.PlayerId == p1).RemainingScore.Should().Be(25);

        await game.UndoLastThrow(CancellationToken.None); // undo the busting dart

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p1); // turn ownership reverts, not advanced to P2
        var payload = (X01StatePayload)state.Payload!;
        payload.Players.Single(p => p.PlayerId == p1).RemainingScore.Should().Be(5); // dart 1's effect restored
        payload.CurrentVisitThrows.Should().HaveCount(1);
    }

    [Fact]
    public async Task Undo_of_the_match_winning_dart_uncompletes_the_match()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string>
        {
            ["startingScore"] = "40",
            ["legsToWin"] = "1",
            ["setsToWin"] = "1",
        });

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None);
        (await game.GetState()).IsComplete.Should().BeTrue();

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();
        state.WinnerPlayerId.Should().BeNull();
    }

    [Fact]
    public async Task Undo_with_no_throws_throws_rule_violation()
    {
        var game = await X01TestGame.Create([Guid.NewGuid()]);

        var act = () => game.UndoLastThrow(CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
