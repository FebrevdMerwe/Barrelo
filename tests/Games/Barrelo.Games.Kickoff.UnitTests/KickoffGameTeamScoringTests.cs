using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.Kickoff.UnitTests;

public class KickoffGameTeamScoringTests
{
    [Fact]
    public async Task Team_members_share_one_cumulative_goals_and_legs_state()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await KickoffTestGame.Create([a1, b1, a2], groups);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // a1 scores for team A

        var payload = await game.Payload();
        payload.GroupFor(a1).Goals.Should().Be(1);
        payload.GroupFor(a2).Goals.Should().Be(1); // a2 shares a1's goal tally
        payload.GroupFor(b1).Goals.Should().Be(0);
    }

    [Fact]
    public async Task Turns_alternate_strictly_between_the_two_sides_regardless_of_uneven_roster_size()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var a3 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [a3] = 0, [b1] = 1 };
        var game = await KickoffTestGame.Create([a1, a2, a3, b1], groups);

        // Side A (3 players) vs side B (1 player): possession must still flip every visit, never
        // letting side A go twice in a row just because it has more players.
        (await game.GetState()).CurrentPlayerId.Should().Be(a1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(a2); // side A's rotation moves to its next member
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b1); // side B only has one member
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(a3);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(b1);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        (await game.GetState()).CurrentPlayerId.Should().Be(a1); // side A's rotation has wrapped around
    }

    [Fact]
    public async Task An_own_goal_still_advances_the_scoring_sides_own_rotation()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await KickoffTestGame.Create([a1, b1, a2], groups);

        (await game.GetState()).CurrentPlayerId.Should().Be(a1);

        await game.ReceiveThrow(KickoffTestGame.WestGoalKick(), CancellationToken.None); // a1 own-goals

        // Side A keeps the ball (they conceded), but it's a2's turn now, not a1's again.
        (await game.GetState()).CurrentPlayerId.Should().Be(a2);
    }

    [Fact]
    public async Task Team_win_credits_every_member_even_if_only_one_scored()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var options = new Dictionary<string, string> { ["goalsToWinLeg"] = "1", ["legsToWinMatch"] = "1" };
        var game = await KickoffTestGame.Create([a1, b1, a2], groups, options);

        await game.ReceiveThrow(KickoffTestGame.EastGoalKick(), CancellationToken.None); // a1 scores the winner

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([a1, a2]);
    }
}
