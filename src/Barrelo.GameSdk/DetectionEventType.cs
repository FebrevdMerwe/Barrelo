namespace Barrelo.GameSdk;

public enum DetectionEventType
{
    /// <summary>One newly detected dart.</summary>
    Throw,

    EndOfTurn,

    /// <summary>The complete set of darts detected in the current visit so far, replacing whatever was
    /// previously known rather than adding to it. Detectors that report a whole visit at a time (AutoDarts
    /// resends a growing array on every dart) use this: an absolute statement self-corrects after a
    /// dropped or duplicated message, where "here is one more dart" cannot.</summary>
    VisitUpdated,
}
