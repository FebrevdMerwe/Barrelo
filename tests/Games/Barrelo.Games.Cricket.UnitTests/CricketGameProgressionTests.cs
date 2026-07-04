using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Cricket.UnitTests;

public class CricketGameProgressionTests
{
    [Fact]
    public async Task Triple_hit_closes_a_number_in_one_dart_with_no_score()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);

        var payload = await game.Payload();
        payload.GroupFor(p1).Marks[0].Should().Be(3);
        payload.GroupFor(p1).Points.Should().Be(0);
    }

    [Fact]
    public async Task Hitting_an_already_closed_number_scores_while_an_opponent_is_still_open()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 closes 20
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> P2
        await game.ReceiveEndOfTurn(CancellationToken.None); // P2 does nothing, -> P1
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 scores since P2 is still open on 20

        var payload = await game.Payload();
        payload.GroupFor(p1).Points.Should().Be(60);
        payload.GroupFor(p1).Marks[0].Should().Be(3);
    }

    [Fact]
    public async Task Marks_fill_across_multiple_darts_with_no_overflow_when_exact()
    {
        var p1 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // 2 marks
        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 20), CancellationToken.None);  // 1 mark closes it exactly

        var payload = await game.Payload();
        payload.GroupFor(p1).Marks[0].Should().Be(3);
        payload.GroupFor(p1).Points.Should().Be(0);
    }

    [Fact]
    public async Task Non_target_segments_and_misses_have_no_effect_but_still_consume_a_dart()
    {
        var p1 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 7), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var payload = await game.Payload();
        payload.Groups.Single().Marks.Should().OnlyContain(m => m == 0);
        payload.CurrentVisitThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Turn_advances_after_three_darts()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 7), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 7), CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(p1);
        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 7), CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
    }

    [Fact]
    public async Task ReceiveEndOfTurn_advances_turn_mid_visit()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p2);
        (await game.Payload()).CurrentVisitThrows.Should().BeEmpty();
    }
}
