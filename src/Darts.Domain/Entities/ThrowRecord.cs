using Darts.Domain.Common;
using Darts.Domain.Enums;

namespace Darts.Domain.Entities;

public sealed class ThrowRecord : Entity<Guid>
{
    public Guid MatchId { get; private set; }

    public Guid PlayerId { get; private set; }

    public int SetNumber { get; private set; }

    public int LegNumber { get; private set; }

    public int Sequence { get; private set; }

    public int Segment { get; private set; }

    public Ring Ring { get; private set; }

    public int Score { get; private set; }

    public string RawNotation { get; private set; } = string.Empty;

    public double PositionX { get; private set; }

    public double PositionY { get; private set; }

    public DetectionSource Source { get; private set; }

    public DateTimeOffset DetectedAtUtc { get; private set; }

    private ThrowRecord()
    {
    }

    public static ThrowRecord Create(
        Guid matchId,
        Guid playerId,
        int setNumber,
        int legNumber,
        int sequence,
        int segment,
        Ring ring,
        int score,
        string rawNotation,
        double positionX,
        double positionY,
        DetectionSource source,
        DateTimeOffset detectedAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = matchId,
        PlayerId = playerId,
        SetNumber = setNumber,
        LegNumber = legNumber,
        Sequence = sequence,
        Segment = segment,
        Ring = ring,
        Score = score,
        RawNotation = rawNotation,
        PositionX = positionX,
        PositionY = positionY,
        Source = source,
        DetectedAtUtc = detectedAtUtc,
    };
}
