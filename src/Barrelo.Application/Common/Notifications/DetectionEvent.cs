using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Notifications;

/// <summary>Application-side envelope a detection source yields — the wire shape between IDetectionSource and the command dispatch layer.</summary>
public sealed record DetectionEvent(DetectionEventType Type, string BoardId, DetectedThrow? Throw);
