using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Games.AroundTheClock.UnitTests;

public class AroundTheClockGameScoringRulesTests
{
    // Ring.Single is the 25-ring and only ever pairs with segment 25, so it has no place among the
    // numbered rings here — see DartScoring.IsBull and BoardGeometry.RadialBand.
    [Theory]
    [InlineData(Ring.OuterSingle, 1)]
    [InlineData(Ring.InnerSingle, 1)]
    [InlineData(Ring.Double, 2)]
    [InlineData(Ring.Triple, 3)]
    public async Task Ring_on_the_target_number_decides_how_many_rungs_are_climbed(Ring ring, int expected)
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(ring, 1), CancellationToken.None); // everyone opens on 1

        (await game.GroupOf(p1)).Progress.Should().Be(expected);
    }

    [Fact]
    public async Task The_next_target_follows_the_rung_actually_reached()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None);

        (await game.GroupOf(p1)).TargetNumber.Should().Be(4); // 1 -> 2 -> 3 -> now on 4
    }

    [Fact]
    public async Task Wrong_number_and_miss_consume_a_dart_without_moving()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(0);
        (await game.Payload()).CurrentVisitThrows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Advances_stack_within_a_single_visit()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None); // +3, now on 4
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);      // +0
        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 4), CancellationToken.None); // +2

        (await game.GroupOf(p1)).Progress.Should().Be(5);
    }

    [Fact]
    public async Task Steps_are_reported_per_dart_while_the_visit_is_still_open()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 1), CancellationToken.None);
        await game.ReceiveThrow(TestThrow.Of(Ring.Miss), CancellationToken.None);

        var payload = await game.Payload();
        payload.CurrentVisitSteps.Should().Equal(3, 0);
    }

    [Fact]
    public async Task A_treble_at_the_top_of_the_clock_is_clamped_onto_the_bull_not_through_it()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(18); // on 19

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 19), CancellationToken.None);

        var group = await game.GroupOf(p1);
        group.Progress.Should().Be(AroundTheClockTestGame.BullStep);
        group.TargetIsBull.Should().BeTrue();
        group.IsFinished.Should().BeFalse();
        (await game.GetState()).IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task A_double_on_twenty_lands_on_the_bull_rather_than_winning()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(19); // on 20

        await game.ReceiveThrow(TestThrow.Of(Ring.Double, 20), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(AroundTheClockTestGame.BullStep);
        (await game.GetState()).IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Outer_bull_does_nothing_on_the_bull_step()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(AroundTheClockTestGame.BullStep);

        await game.ReceiveThrow(TestThrow.OuterBull(), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(AroundTheClockTestGame.BullStep);
        (await game.GetState()).IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Numbers_do_nothing_once_a_team_is_on_the_bull()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(AroundTheClockTestGame.BullStep);

        await game.ReceiveThrow(TestThrow.Of(Ring.Triple, 20), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(AroundTheClockTestGame.BullStep);
    }

    [Fact]
    public async Task Inner_bull_finishes_the_clock()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ClimbSolo(AroundTheClockTestGame.BullStep);

        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        (await game.GroupOf(p1)).IsFinished.Should().BeTrue();
        (await game.GetState()).IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task A_bull_thrown_before_the_bull_step_counts_for_nothing()
    {
        var p1 = Guid.NewGuid();
        var game = await AroundTheClockTestGame.Create([p1]);

        await game.ReceiveThrow(TestThrow.InnerBull(), CancellationToken.None);

        (await game.GroupOf(p1)).Progress.Should().Be(0);
        (await game.GetState()).IsComplete.Should().BeFalse();
    }
}
