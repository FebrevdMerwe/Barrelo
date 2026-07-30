using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.X01.UnitTests;

public class X01GameProgressionTests
{
    private static readonly Dictionary<string, string> QuickFinishOptions = new()
    {
        ["startingScore"] = "40",
        ["legsToWin"] = "2",
        ["setsToWin"] = "1",
    };

    [Fact]
    public async Task Leg_win_starts_a_new_leg_with_alternating_start_player()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2], QuickFinishOptions);

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // P1 checks out leg 1

        var state = await game.GetState();
        state.LegNumber.Should().Be(2);
        state.CurrentPlayerId.Should().Be(p2); // leg 2 starts with the other player
        var payload = (X01StatePayload)state.Payload!;
        payload.Groups.Should().OnlyContain(g => g.RemainingScore == 40);
    }

    [Fact]
    public async Task Reaching_legsToWin_wins_the_set_and_match()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2], QuickFinishOptions);

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // P1 wins leg 1
        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // P2 wins leg 2
        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // P1 wins leg 3 -> 2 legs -> match

        var state = await game.GetState();
        state.IsComplete.Should().BeTrue();
        state.Status.Should().Be(GameStatus.Complete);
        state.WinnerPlayerIds.Should().BeEquivalentTo([p1]);
        state.CurrentPlayerId.Should().BeNull();
    }

    [Fact]
    public async Task Explicit_end_of_turn_after_a_completed_visit_does_not_double_advance()
    {
        // Detection sources (e.g. AutoDarts) fire an EndOfTurn event once the board is cleared,
        // regardless of whether the visit already auto-ended (3 darts thrown, bust, or checkout).
        // That EndOfTurn must be a no-op in that case, or the turn skips straight past the next
        // player and lands back on the one who just threw.
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None); // 3rd dart auto-advances to p2

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);

        await game.ReceiveEndOfTurn(CancellationToken.None); // hardware confirms the same visit ended

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
    }

    [Fact]
    public async Task Explicit_end_of_turn_after_a_bust_does_not_double_advance()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2], new Dictionary<string, string> { ["startingScore"] = "5" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // busts: 5-60<0, auto-advances to p2

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);

        await game.ReceiveEndOfTurn(CancellationToken.None); // hardware confirms the same visit ended

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
    }

    [Fact]
    public async Task Cannot_receive_further_throws_after_match_completes()
    {
        var p1 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1], new Dictionary<string, string>
        {
            ["startingScore"] = "40",
            ["legsToWin"] = "1",
            ["setsToWin"] = "1",
        });

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // wins the match outright

        var act = () => game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }
}
