using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameProgressionTests
{
    [Fact]
    public async Task Three_darts_do_not_hand_over_the_turn_on_their_own()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        // The oche changes hands when the darts come out of the board, not when the third one goes in —
        // a detection source reports that takeout as EndOfTurn.
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var state = await game.GetState();
        state.CurrentPlayerId.Should().Be(p1);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(3);
    }

    [Fact]
    public async Task End_of_turn_after_a_full_visit_hands_over_exactly_once()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
        (await game.Payload()).CurrentVisitThrows.Should().BeEmpty();
    }

    [Fact]
    public async Task A_fourth_dart_before_end_of_turn_is_refused()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, Guid.NewGuid()]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var act = () => game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
        (await game.GroupOf(p1)).Progress.Should().Be(0); // and it scored nothing on the way out
    }

    [Fact]
    public async Task ReceiveEndOfTurn_advances_turn_mid_visit_and_clears_the_visit()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.OuterSingle, 1), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        var payload = await game.Payload();
        (await game.GetState()).CurrentPlayerId.Should().Be(p2);
        payload.CurrentVisitThrows.Should().BeEmpty();
        payload.CurrentVisitSteps.Should().BeEmpty();
        payload.GroupFor(p1).Progress.Should().Be(1); // the dart itself still counted
    }

    [Fact]
    public async Task Round_ticks_only_once_the_rotation_returns_to_the_first_team()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2, p3]);

        (await game.Payload()).Round.Should().Be(1);

        await game.ReceiveEndOfTurn(CancellationToken.None); // -> p2
        (await game.Payload()).Round.Should().Be(1);
        await game.ReceiveEndOfTurn(CancellationToken.None); // -> p3
        (await game.Payload()).Round.Should().Be(1);
        await game.ReceiveEndOfTurn(CancellationToken.None); // wraps -> p1

        var payload = await game.Payload();
        payload.Round.Should().Be(2);
        (await game.GetState()).CurrentPlayerId.Should().Be(p1);
    }

    [Fact]
    public async Task RecentThrows_accumulates_every_dart_of_the_match()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1, p2]);

        await game.ReceiveThrow(TestThrow.Of(Ring.OuterSingle, 1), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        (await game.GetState()).RecentThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_team_on_the_bull_reports_a_null_target_number()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(AroundTheClockTestGame.BullStep);

        var group = await game.GroupOf(p1);
        group.TargetNumber.Should().BeNull();
        group.TargetIsBull.Should().BeTrue();
    }
}
