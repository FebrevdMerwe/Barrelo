using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.X01.UnitTests;

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
