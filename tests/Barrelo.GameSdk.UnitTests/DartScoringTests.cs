using FluentAssertions;

namespace Barrelo.GameSdk.UnitTests;

public class DartScoringTests
{
    [Theory]
    [InlineData(Ring.Miss, 20, 0)]
    [InlineData(Ring.InnerSingle, 20, 20)]
    [InlineData(Ring.OuterSingle, 20, 20)]
    [InlineData(Ring.Triple, 20, 60)]
    [InlineData(Ring.Double, 20, 40)]
    [InlineData(Ring.Triple, 1, 3)]
    [InlineData(Ring.Double, 25, 50)]
    [InlineData(Ring.Single, 25, 25)]
    public void Score_returns_expected_value(Ring ring, int segment, int expected)
    {
        DartScoring.Score(ring, segment).Should().Be(expected);
    }

    [Fact]
    public void Score_distinguishes_double_bull_from_single_bull()
    {
        DartScoring.Score(Ring.Double, 25).Should().Be(50);
        DartScoring.Score(Ring.Single, 25).Should().Be(25);
    }

    [Theory]
    [InlineData(Ring.Miss, 20, "MISS")]
    [InlineData(Ring.InnerSingle, 20, "20")]
    [InlineData(Ring.OuterSingle, 20, "20")]
    [InlineData(Ring.Triple, 20, "T20")]
    [InlineData(Ring.Double, 20, "D20")]
    [InlineData(Ring.Double, 25, "BULL")]
    [InlineData(Ring.Single, 25, "25")]
    public void Notation_returns_expected_token(Ring ring, int segment, string expected)
    {
        DartScoring.Notation(ring, segment).Should().Be(expected);
    }

    [Theory]
    [InlineData(Ring.Double, true)]
    [InlineData(Ring.Single, false)]
    [InlineData(Ring.Triple, false)]
    [InlineData(Ring.InnerSingle, false)]
    [InlineData(Ring.OuterSingle, false)]
    [InlineData(Ring.Miss, false)]
    public void IsValidCheckoutRing_accepts_only_double_including_bull(Ring ring, bool expected)
    {
        // Ring.Double alone qualifies regardless of segment — at segment 25 that's the bull-double
        // (50), which counts as a valid double-out finish exactly like any wedge double.
        DartScoring.IsValidCheckoutRing(ring).Should().Be(expected);
    }

    [Theory]
    [InlineData(Ring.Single, 25, true)]
    [InlineData(Ring.Double, 25, true)]
    [InlineData(Ring.Double, 20, false)]
    [InlineData(Ring.Triple, 20, false)]
    [InlineData(Ring.InnerSingle, 20, false)]
    [InlineData(Ring.OuterSingle, 20, false)]
    [InlineData(Ring.Miss, 0, false)]
    public void IsBull_identifies_only_the_25_point_ring(Ring ring, int segment, bool expected)
    {
        DartScoring.IsBull(ring, segment).Should().Be(expected);
    }
}
