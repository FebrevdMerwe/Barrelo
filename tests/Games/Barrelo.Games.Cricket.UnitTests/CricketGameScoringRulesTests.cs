using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Cricket.UnitTests;

public class CricketGameScoringRulesTests
{
    [Fact]
    public async Task Overflow_does_not_score_once_every_other_group_has_also_closed_the_number()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 closes 20
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P2 closes 20 too — now dead
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 hits 20 again — no one left to score off

        var payload = await game.Payload();
        payload.GroupFor(p1).Points.Should().Be(0);
    }

    [Fact]
    public async Task Overflow_scores_if_any_other_group_remains_open_even_if_a_third_has_closed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a] = 0, [b] = 1, [c] = 2 };
        var game = await CricketTestGame.Create([a, b, c], groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A closes 20
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> B
        await game.ReceiveEndOfTurn(CancellationToken.None); // B does nothing, -> C
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // C closes 20 too
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A scores — B is still open

        var payload = await game.Payload();
        payload.GroupFor(a).Points.Should().Be(60);
    }
}
