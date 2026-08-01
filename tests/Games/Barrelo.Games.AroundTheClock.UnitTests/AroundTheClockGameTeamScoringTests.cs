using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameTeamScoringTests
{
    [Fact]
    public async Task Team_mates_climb_one_shared_clock()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await AroundTheClockTestGame.Create([a1, a2, b1], groups);

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 1), CancellationToken.None); // a1 takes the team to 3
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> team B
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> team A, now a2 throws

        (await game.GetState()).CurrentPlayerId.Should().Be(a2);
        var group = await game.GroupOf(a2);
        group.Progress.Should().Be(2);
        group.TargetNumber.Should().Be(3); // a2 picks up exactly where a1 left off
    }

    [Fact]
    public async Task Each_team_gets_one_visit_per_round_regardless_of_size()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var a3 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var b2 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [a3] = 0, [b1] = 1, [b2] = 1 };
        var game = await AroundTheClockTestGame.Create([a1, a2, a3, b1, b2], groups);

        var seen = new List<Guid>();
        for (var visit = 0; visit < 6; visit++)
        {
            seen.Add((await game.GetState()).CurrentPlayerId!.Value);
            await game.ReceiveEndOfTurn(CancellationToken.None);
        }

        // Sides alternate strictly — a three-player team never out-throws a two-player one — while each
        // side rotates its own thrower independently.
        seen.Should().Equal(a1, b1, a2, b2, a3, b1);
    }

    [Fact]
    public async Task Ungrouped_players_each_become_their_own_team()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        var payload = await game.Payload();

        payload.Groups.Should().HaveCount(2);
        payload.Groups.Should().OnlyContain(g => g.PlayerIds.Count == 1);
    }

    [Fact]
    public async Task A_win_belongs_to_the_whole_team_not_the_thrower()
    {
        var a1 = Guid.NewGuid();
        var a2 = Guid.NewGuid();
        var b1 = Guid.NewGuid();
        var groups = new Dictionary<Guid, int> { [a1] = 0, [a2] = 0, [b1] = 1 };
        var game = await AroundTheClockTestGame.Create([a1, a2, b1], groups);

        await game.ClimbToBull(a1);
        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.WinnerPlayerIds.Should().BeEquivalentTo([a1, a2]);
    }
}
