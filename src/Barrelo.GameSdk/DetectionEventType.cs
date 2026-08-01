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

    /// <summary>The detector came up or went away. Carried on the same stream as darts so the one consumer
    /// sees connection changes in order with the throws around them — a board that drops mid-visit and a
    /// board that drops between visits need different handling, and only the ordering says which happened.</summary>
    ConnectionChanged,
}
