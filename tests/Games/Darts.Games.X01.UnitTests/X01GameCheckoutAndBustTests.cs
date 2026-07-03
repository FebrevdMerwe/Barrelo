using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Games.X01.UnitTests;

public class X01GameCheckoutAndBustTests
{
    [Fact]
    public async Task Overthrow_busts_and_reverts_remaining_score()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "5" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None); // scores 60, overshoots 5

        var payload = await game.Payload();
        payload.Groups.Single().RemainingScore.Should().Be(5);
        payload.CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task Remaining_one_under_double_out_busts()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "21" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Outer, 20), CancellationToken.None); // scores 20, leaves 1

        var payload = await game.Payload();
        payload.Groups.Single().RemainingScore.Should().Be(21);
    }

    [Fact]
    public async Task Double_checkout_wins_the_leg()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "40" });

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None); // D20 = 40

        var payload = await game.Payload();
        payload.Groups.Single().RemainingScore.Should().Be(40); // leg reset for the (single) player's next leg
        payload.Groups.Single().LegsWon.Should().Be(1);
    }

    [Fact]
    public async Task Inner_bull_double_bull_finish_wins_the_leg()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "50" });

        await game.ReceiveThrow(TestThrow.Of(Ring.InnerBull), CancellationToken.None); // 50

        var payload = await game.Payload();
        payload.Groups.Single().LegsWon.Should().Be(1);
    }

    [Fact]
    public async Task Outer_bull_is_not_a_valid_double_out_finish_and_busts()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create([player], new Dictionary<string, string> { ["startingScore"] = "25" });

        await game.ReceiveThrow(TestThrow.Of(Ring.OuterBull), CancellationToken.None); // 25, but not a double

        var payload = await game.Payload();
        payload.Groups.Single().RemainingScore.Should().Be(25);
        payload.Groups.Single().LegsWon.Should().Be(0);
    }

    [Fact]
    public async Task Straight_out_mode_allows_any_ring_to_finish()
    {
        var player = Guid.NewGuid();
        var game = await X01TestGame.Create(
            [player],
            new Dictionary<string, string> { ["startingScore"] = "25", ["doubleOut"] = "false" });

        await game.ReceiveThrow(TestThrow.Of(Ring.OuterBull), CancellationToken.None); // 25, straight out

        var payload = await game.Payload();
        payload.Groups.Single().LegsWon.Should().Be(1);
    }
}
