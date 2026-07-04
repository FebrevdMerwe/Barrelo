using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Cricket.UnitTests;

public class CricketGameWinConditionTests
{
    [Fact]
    public async Task Closing_all_targets_while_tied_does_not_end_the_match_until_taking_the_lead()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2]); // implicit groups: p1=0, p2=1

        foreach (var segment in new[] { 20, 19, 18, 17, 16, 15 })
        {
            await game.ReceiveThrow(TestThrow.Of(Ring.Triple, segment), CancellationToken.None); // P1 closes exactly, 0 points
            await game.ReceiveEndOfTurn(CancellationToken.None); // -> P2
            await game.ReceiveEndOfTurn(CancellationToken.None); // P2 does nothing, -> P1
        }
        await game.ReceiveThrow(TestThrow.Of(Ring.OuterBull), CancellationToken.None); // bull mark 1/3
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> P2
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> P1
        await game.ReceiveThrow(TestThrow.Of(Ring.InnerBull), CancellationToken.None); // bull closed (3/3), still tied 0-0

        var tied = await game.GetState();
        tied.IsComplete.Should().BeFalse();
        var tiedPayload = (CricketStatePayload)tied.Payload!;
        tiedPayload.GroupFor(p1).ClosedCount.Should().Be(7);
        tiedPayload.GroupFor(p1).Points.Should().Be(0);

        await game.ReceiveEndOfTurn(CancellationToken.None); // -> P2
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> P1
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // P1 scores off closed 20 — P2 never touched it, takes the lead

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([p1]);
    }

    [Fact]
    public async Task GetResult_before_completion_throws()
    {
        var game = await CricketTestGame.Create([Guid.NewGuid(), Guid.NewGuid()]);

        var act = () => game.GetResult();

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task FinalStandings_orders_non_winners_by_closed_count_then_points()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var game = await CricketTestGame.Create([p1, p2, p3]); // implicit groups: p1=0, p2=1, p3=2

        var p1Actions = new[]
        {
            TestThrow.Of(Ring.Triple, 20),
            TestThrow.Of(Ring.Triple, 19),
            TestThrow.Of(Ring.Triple, 18),
            TestThrow.Of(Ring.Triple, 17),
            TestThrow.Of(Ring.Triple, 16),
            TestThrow.Of(Ring.Triple, 15),
            TestThrow.Of(Ring.InnerBull), // bull 2/3, no overflow
            TestThrow.Of(Ring.InnerBull), // bull closes (3/3) + 25 overflow, still tied with P3
            TestThrow.Of(Ring.InnerBull), // +50 more overflow — takes the outright lead, wins
        };
        var p2Actions = new DetectedThrow?[]
        {
            TestThrow.Of(Ring.Triple, 19), // closes exactly, 0 points
            TestThrow.Of(Ring.Triple, 18), // closes exactly, 0 points
            null, null, null, null, null, null,
        };
        var p3Actions = new DetectedThrow?[]
        {
            TestThrow.Of(Ring.InnerBull), // bull 2/3, no overflow
            TestThrow.Of(Ring.InnerBull), // bull closes (3/3) + 25 overflow
            null, null, null, null, null, null,
        };

        for (var round = 0; round < 8; round++)
        {
            await game.ReceiveThrow(p1Actions[round], CancellationToken.None);
            await game.ReceiveEndOfTurn(CancellationToken.None);

            if (p2Actions[round] is { } p2Throw)
                await game.ReceiveThrow(p2Throw, CancellationToken.None);
            await game.ReceiveEndOfTurn(CancellationToken.None);

            if (p3Actions[round] is { } p3Throw)
                await game.ReceiveThrow(p3Throw, CancellationToken.None);
            await game.ReceiveEndOfTurn(CancellationToken.None);
        }

        await game.ReceiveThrow(p1Actions[8], CancellationToken.None); // P1's winning dart

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([p1]);

        var result = await game.GetResult();
        result.WinnerPlayerIds.Should().BeEquivalentTo([p1]);
        // P2 (2 closed, 0 pts) ranks above P3 (1 closed, 25 pts) despite fewer points — ClosedCount is primary.
        result.FinalStandings.Should().Equal(p1, p2, p3);
    }
}
