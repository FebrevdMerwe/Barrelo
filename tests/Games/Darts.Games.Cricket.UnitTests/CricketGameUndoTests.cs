using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.Cricket.UnitTests;

public class CricketGameUndoTests
{
    [Fact]
    public async Task Undo_mid_visit_reverts_the_last_throw()
    {
        var p1 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);
        await game.UndoLastThrow(CancellationToken.None);

        var payload = await game.Payload();
        payload.Groups.Single().Marks[0].Should().Be(0);
        payload.CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task Undo_of_a_scoring_overflow_dart_reverts_points_but_keeps_the_earlier_close()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 closes 20
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 scores 60

        (await game.Payload()).GroupFor(p1).Points.Should().Be(60);

        await game.UndoLastThrow(CancellationToken.None); // undo the scoring dart only

        var payload = await game.Payload();
        payload.GroupFor(p1).Points.Should().Be(0);
        payload.GroupFor(p1).Marks[0].Should().Be(3); // the earlier close still stands
    }

    [Fact]
    public async Task Undo_of_the_third_dart_in_a_visit_restores_turn_ownership()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None); // 3rd dart auto-advances the turn

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);

        await game.UndoLastThrow(CancellationToken.None); // undo the 3rd dart

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p1); // turn ownership reverts, not advanced to P2
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Undo_of_the_winning_dart_uncompletes_the_match()
    {
        var p1 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1]); // solo game: wins the instant it closes everything

        foreach (var segment in new[] { 20, 19, 18, 17, 16, 15 })
            await game.ReceiveThrow(TestThrow.Of(Ring.Triple, segment), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.OuterBull), CancellationToken.None); // bull 1/3
        await game.ReceiveThrow(TestThrow.Of(Ring.InnerBull), CancellationToken.None); // bull closes -> wins outright

        (await game.GetState()).IsComplete.Should().BeTrue();

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();
        state.WinnerPlayerIds.Should().BeNull();
        (await game.Payload()).Groups.Single().Marks[6].Should().Be(1); // only the OuterBull mark remains
    }

    [Fact]
    public async Task Undo_with_no_throws_throws_rule_violation()
    {
        var game = await CricketTestGame.Create([Guid.NewGuid()]);

        var act = () => game.UndoLastThrow(CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
