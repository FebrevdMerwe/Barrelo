using FluentAssertions;

namespace Barrelo.GameSdk.UnitTests;

public class DartScoringTests
{
    [Theory]
    [InlineData(Ring.Miss, 20, 0)]
    [InlineData(Ring.Inner, 20, 20)]
    [InlineData(Ring.Outer, 20, 20)]
    [InlineData(Ring.Triple, 20, 60)]
    [InlineData(Ring.Double, 20, 40)]
    [InlineData(Ring.Triple, 1, 3)]
    [InlineData(Ring.InnerBull, 0, 50)]
    [InlineData(Ring.OuterBull, 0, 25)]
    public void Score_returns_expected_value(Ring ring, int segment, int expected)
    {
        DartScoring.Score(ring, segment).Should().Be(expected);
    }

    [Fact]
    public void Score_distinguishes_inner_bull_from_outer_bull()
    {
        DartScoring.Score(Ring.InnerBull, 0).Should().Be(50);
        DartScoring.Score(Ring.OuterBull, 0).Should().Be(25);
    }

    [Theory]
    [InlineData(Ring.Miss, 20, "MISS")]
    [InlineData(Ring.Inner, 20, "20")]
    [InlineData(Ring.Outer, 20, "20")]
    [InlineData(Ring.Triple, 20, "T20")]
    [InlineData(Ring.Double, 20, "D20")]
    [InlineData(Ring.InnerBull, 0, "BULL")]
    [InlineData(Ring.OuterBull, 0, "25")]
    public void Notation_returns_expected_token(Ring ring, int segment, string expected)
    {
        DartScoring.Notation(ring, segment).Should().Be(expected);
    }

    [Theory]
    [InlineData(Ring.Double, true)]
    [InlineData(Ring.InnerBull, true)]
    [InlineData(Ring.OuterBull, false)]
    [InlineData(Ring.Triple, false)]
    [InlineData(Ring.Inner, false)]
    [InlineData(Ring.Outer, false)]
    [InlineData(Ring.Miss, false)]
    public void IsValidCheckoutRing_accepts_only_double_and_inner_bull(Ring ring, bool expected)
    {
        DartScoring.IsValidCheckoutRing(ring).Should().Be(expected);
    }
}
