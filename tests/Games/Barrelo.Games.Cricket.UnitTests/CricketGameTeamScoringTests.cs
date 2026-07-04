using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Cricket.UnitTests;

public class CricketGameTeamScoringTests
{
    [Fact]
    public async Task Group_members_share_one_cumulative_marks_and_points_state()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await CricketTestGame.Create([a1, b1, a2], groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A1 throws for team A

        var payload = await game.Payload();
        payload.GroupFor(a1).Marks[0].Should().Be(3);
        payload.GroupFor(a2).Marks[0].Should().Be(3); // A2 shares A1's marks
        payload.GroupFor(b1).Marks[0].Should().Be(0); // team B untouched
    }

    [Fact]
    public async Task Turns_interleave_round_robin_across_groups_by_participant_order()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1, [b2] = 1 };
        var game = await CricketTestGame.Create([a1, b1, a2, b2], groups);

        (await game.GetState()).CurrentPlayerId.Should().Be(a1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(a2);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b2);
    }

    [Fact]
    public async Task Group_win_credits_every_member_even_if_only_one_threw_the_winning_dart()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        // Turn order is [a1, b1, a2]; team A (a1+a2) closes all 7 targets while B never scores,
        // then one more overflow hit takes team A's shared points from a tie (0-0) to an outright lead.
        var game = await CricketTestGame.Create([a1, b1, a2], groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A1 closes 20
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> B1
        await game.ReceiveEndOfTurn(CancellationToken.None); // B1 does nothing, -> A2
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 19), CancellationToken.None); // A2 closes 19 (shared group)
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A1
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 18), CancellationToken.None); // A1 closes 18
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> B1
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A2
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 17), CancellationToken.None); // A2 closes 17
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A1
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 16), CancellationToken.None); // A1 closes 16
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> B1
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A2
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 15), CancellationToken.None); // A2 closes 15
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A1
        await game.ReceiveThrow(TestThrow.Of(Ring.OuterBull), CancellationToken.None); // A1: bull mark 1/3
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> B1
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A2
        await game.ReceiveThrow(TestThrow.Of(Ring.InnerBull), CancellationToken.None); // A2: bull closed (3/3), still tied 0-0

        var beforeLead = await game.GetState();
        beforeLead.IsComplete.Should().BeFalse(); // closed everything but tied — not a win yet

        await game.ReceiveEndOfTurn(CancellationToken.None); // -> A1
        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A1 scores off the already-closed 20 — B never touched it

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([a1, a2]);
    }
}
