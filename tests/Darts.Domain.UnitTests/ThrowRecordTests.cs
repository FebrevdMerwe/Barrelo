using Darts.Domain.Entities;
using Darts.Domain.Enums;
using FluentAssertions;

namespace Darts.Domain.UnitTests;

public class ThrowRecordTests
{
    [Fact]
    public void Create_populates_all_fields()
    {
        var matchId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var detectedAt = DateTimeOffset.UtcNow;

        var record = ThrowRecord.Create(
            matchId, playerId, setNumber: 1, legNumber: 2, sequence: 3,
            segment: 20, ring: Ring.Triple, score: 60, rawNotation: "T20",
            positionX: 0.1, positionY: 0.91,
            source: DetectionSource.Manual, detectedAtUtc: detectedAt);

        record.Id.Should().NotBeEmpty();
        record.MatchId.Should().Be(matchId);
        record.PlayerId.Should().Be(playerId);
        record.SetNumber.Should().Be(1);
        record.LegNumber.Should().Be(2);
        record.Sequence.Should().Be(3);
        record.Segment.Should().Be(20);
        record.Ring.Should().Be(Ring.Triple);
        record.Score.Should().Be(60);
        record.RawNotation.Should().Be("T20");
        record.PositionX.Should().Be(0.1);
        record.PositionY.Should().Be(0.91);
        record.Source.Should().Be(DetectionSource.Manual);
        record.DetectedAtUtc.Should().Be(detectedAt);
    }
}
