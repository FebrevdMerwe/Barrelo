using FluentAssertions;

namespace Darts.GameSdk.UnitTests;

public class BoardGeometryTests
{
    public static IEnumerable<object[]> AllSegments =>
        Enumerable.Range(1, 20).Select(segment => new object[] { segment });

    [Theory]
    [MemberData(nameof(AllSegments))]
    public void CenterOf_inner_stays_within_inner_band(int segment)
    {
        var position = BoardGeometry.CenterOf(segment, Ring.Inner);
        var radius = Magnitude(position);
        radius.Should().BeInRange(15.0 / 100.0, 58.0 / 100.0);
    }

    [Theory]
    [MemberData(nameof(AllSegments))]
    public void CenterOf_triple_stays_within_triple_band(int segment)
    {
        var position = BoardGeometry.CenterOf(segment, Ring.Triple);
        var radius = Magnitude(position);
        radius.Should().BeInRange(58.0 / 100.0, 64.0 / 100.0);
    }

    [Theory]
    [MemberData(nameof(AllSegments))]
    public void CenterOf_outer_stays_within_outer_band(int segment)
    {
        var position = BoardGeometry.CenterOf(segment, Ring.Outer);
        var radius = Magnitude(position);
        radius.Should().BeInRange(64.0 / 100.0, 94.0 / 100.0);
    }

    [Theory]
    [MemberData(nameof(AllSegments))]
    public void CenterOf_double_stays_within_double_band(int segment)
    {
        var position = BoardGeometry.CenterOf(segment, Ring.Double);
        var radius = Magnitude(position);
        radius.Should().BeInRange(94.0 / 100.0, 1.0);
    }

    [Fact]
    public void CenterOf_inner_bull_does_not_throw_regardless_of_segment()
    {
        var position = BoardGeometry.CenterOf(segment: 0, Ring.InnerBull);
        Magnitude(position).Should().BeInRange(0.0, 6.0 / 100.0);
    }

    [Fact]
    public void CenterOf_outer_bull_does_not_throw_regardless_of_segment()
    {
        var position = BoardGeometry.CenterOf(segment: 0, Ring.OuterBull);
        Magnitude(position).Should().BeInRange(6.0 / 100.0, 15.0 / 100.0);
    }

    [Fact]
    public void CenterOf_miss_does_not_throw_regardless_of_segment_and_lands_outside_the_board()
    {
        var position = BoardGeometry.CenterOf(segment: 0, Ring.Miss);
        Magnitude(position).Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void CenterOf_segment_20_sits_on_the_positive_y_axis()
    {
        var position = BoardGeometry.CenterOf(20, Ring.Double);
        position.X.Should().BeApproximately(0.0, 1e-9);
        position.Y.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void CenterOf_throws_for_segment_outside_1_to_20_on_a_wedge_ring()
    {
        var act = () => BoardGeometry.CenterOf(21, Ring.Double);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static double Magnitude(BoardPosition position) =>
        Math.Sqrt(position.X * position.X + position.Y * position.Y);
}
