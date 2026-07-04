namespace Barrelo.GameSdk;

/// <summary>Canonical throw event shared verbatim between the detection subsystem and game logic.</summary>
public sealed record DetectedThrow(
    Guid ThrowId,
    int Segment,
    Ring Ring,
    int Score,
    string RawNotation,
    BoardPosition Position,
    double? Confidence,
    string BoardId,
    int? CameraIndex,
    DateTimeOffset DetectedAtUtc,
    DetectionSourceType Source);
