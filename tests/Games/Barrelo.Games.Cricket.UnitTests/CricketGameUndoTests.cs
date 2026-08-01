using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Cricket.UnitTests;

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
    public async Task Undo_of_the_end_of_turn_after_a_full_visit_restores_turn_ownership()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);

        await game.UndoLastThrow(CancellationToken.None); // undo the takeout, not a dart

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p1);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(3);
    }

    [Fact]
    public async Task Undo_of_the_winning_dart_uncompletes_the_match()
    {
        var p1 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1]); // solo game: wins the instant it closes everything

        DetectedThrow[] darts =
        [
            TestThrow.Of(Ring.Triple, 20), TestThrow.Of(Ring.Triple, 19), TestThrow.Of(Ring.Triple, 18),
            TestThrow.Of(Ring.Triple, 17), TestThrow.Of(Ring.Triple, 16), TestThrow.Of(Ring.Triple, 15),
            TestThrow.Of(Ring.Single, 25),  // bull 1/3
            TestThrow.Of(Ring.Double, 25),  // bull closes -> wins outright
        ];

        for (var i = 0; i < darts.Length; i++)
        {
            // Nothing ends a visit on dart count, so the takeout between visits has to be explicit.
            if (i > 0 && i % 3 == 0)
                await game.ReceiveEndOfTurn(CancellationToken.None);
            await game.ReceiveThrow(darts[i], CancellationToken.None);
        }

        (await game.GetState()).IsComplete.Should().BeTrue();

        await game.UndoLastThrow(CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeFalse();
        state.WinnerPlayerIds.Should().BeNull();
        (await game.Payload()).Groups.Single().Marks[6].Should().Be(1); // only the single-bull mark remains
    }

    [Fact]
    public async Task Undo_with_no_throws_throws_rule_violation()
    {
        var game = await CricketTestGame.Create([Guid.NewGuid()]);

        var act = () => game.UndoLastThrow(CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
