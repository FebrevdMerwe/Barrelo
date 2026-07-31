using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Notifications;

/// <summary>Application-side envelope a detection source yields — the wire shape between IDetectionSource
/// and the command dispatch layer. <see cref="Throw"/> is populated for
/// <see cref="DetectionEventType.Throw"/>, <see cref="Visit"/> for
/// <see cref="DetectionEventType.VisitUpdated"/>, and neither for
/// <see cref="DetectionEventType.EndOfTurn"/>.</summary>
public sealed record DetectionEvent(
    DetectionEventType Type,
    string BoardId,
    DetectedThrow? Throw,
    IReadOnlyList<DetectedThrow>? Visit = null);
