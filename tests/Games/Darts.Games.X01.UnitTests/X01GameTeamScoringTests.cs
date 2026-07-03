using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.X01.UnitTests;

public class X01GameTeamScoringTests
{
    [Fact]
    public async Task Group_members_share_one_cumulative_score()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1, [b2] = 1 };
        var game = await X01TestGame.Create(
            [a1, b1, a2, b2],
            new Dictionary<string, string> { ["startingScore"] = "100" },
            groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A1 throws for team A: 100-60=40

        var payload = await game.Payload();
        payload.GroupFor(a1).RemainingScore.Should().Be(40);
        payload.GroupFor(a2).RemainingScore.Should().Be(40); // A2 shares A1's score
        payload.GroupFor(b1).RemainingScore.Should().Be(100); // team B untouched
    }

    [Fact]
    public async Task Turns_interleave_round_robin_across_groups_by_participant_order()
    {
        var a1 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1, [b2] = 1 };
        var game = await X01TestGame.Create([a1, b1, a2, b2], playerGroups: groups);

        (await game.GetState()).CurrentPlayerId.Should().Be(a1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(a2);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b2);
    }

    [Fact]
    public async Task Bust_reverts_the_groups_shared_score_not_just_the_throwers()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await X01TestGame.Create(
            [a1, b1, a2],
            new Dictionary<string, string> { ["startingScore"] = "5" },
            groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // A1 busts: 5-60<0

        var payload = await game.Payload();
        payload.GroupFor(a1).RemainingScore.Should().Be(5);
        payload.GroupFor(a2).RemainingScore.Should().Be(5); // reverted for the whole group, not just A1
        (await game.GetState()).CurrentPlayerId.Should().Be(b1); // turn still advances to the other group
    }

    [Fact]
    public async Task Checkout_by_one_member_credits_the_whole_groups_legs_and_reports_both_as_winners()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await X01TestGame.Create(
            [a1, b1, a2],
            new Dictionary<string, string> { ["startingScore"] = "40", ["legsToWin"] = "1", ["setsToWin"] = "1" },
            groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // A1 checks out for team A

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([a1, a2]);

        var payload = (X01StatePayload)state.Payload!;
        payload.GroupFor(a2).LegsWon.Should().Be(1); // A2 credited even though A1 threw the checkout
    }
}
