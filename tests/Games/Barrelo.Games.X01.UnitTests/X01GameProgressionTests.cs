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
    public async Task Three_darts_do_not_hand_over_the_turn_on_their_own()
    {
        // The oche changes hands when the darts come out of the board, not when the third one goes in —
        // a detection source reports that takeout as EndOfTurn, and only that advances the player.
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p1);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(3);

        await game.ReceiveEndOfTurn(CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
    }

    [Fact]
    public async Task A_fourth_dart_before_end_of_turn_is_refused()
    {
        var p1 = Guid.NewGuid();
        var game = await X01TestGame.Create([p1, Guid.NewGuid()]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var act = () => game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
        (await game.Payload()).GroupFor(p1).RemainingScore.Should().Be(501); // and it scored nothing
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
