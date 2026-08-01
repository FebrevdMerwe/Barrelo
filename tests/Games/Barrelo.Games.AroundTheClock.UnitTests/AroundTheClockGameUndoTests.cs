using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameUndoTests
{
    [Fact]
    public async Task Undo_mid_visit_reverts_the_last_throw()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None);
        await game.UndoLastThrow(CancellationToken.None);

        var payload = await game.Payload();
        payload.Groups.Single().Progress.Should().Be(0);
        payload.CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task Undo_of_a_clamped_treble_restores_the_rung_it_came_from()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(18); // on 19
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 19), CancellationToken.None); // clamped to the bull step

        (await game.GroupOf(p1)).Progress.Should().Be(AroundTheClockTestGame.BullStep);

        await game.UndoLastThrow(CancellationToken.None);

        var group = await game.GroupOf(p1);
        group.Progress.Should().Be(18);
        group.TargetNumber.Should().Be(19);
    }

    [Fact]
    public async Task Undo_of_the_third_dart_reopens_the_visit_for_the_same_player()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(3);

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p1); // the turn never left them
        (await game.GroupOf(p1)).Progress.Should().Be(0);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Undo_after_a_refused_fourth_dart_frees_the_slot_again()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, Guid.NewGuid()]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        await game.UndoLastThrow(CancellationToken.None); // take one back...

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None); // ...and the slot is usable

        (await game.GroupOf(p1)).Progress.Should().Be(3);
    }

    [Fact]
    public async Task Undo_of_an_end_of_turn_hands_the_visit_back()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.OuterSingle, 1), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);

        await game.UndoLastThrow(CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p1);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(1);
    }

    [Fact]
    public async Task Undo_of_the_winning_bull_uncompletes_the_match()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ClimbToBull(p1);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        (await game.GetState()).IsComplete.Should().BeTrue();

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();
        state.WinnerPlayerIds.Should().BeNull();
        state.CurrentPlayerId.Should().Be(p1); // still their dart to throw again
        (await game.GroupOf(p1)).Progress.Should().Be(AroundTheClockTestGame.BullStep);
    }

    [Fact]
    public async Task Undo_with_no_throws_throws_rule_violation()
    {
        var game = await AroundTheClockTestGame.Create([Guid.NewGuid()]);

        var act = () => game.UndoLastThrow(CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
