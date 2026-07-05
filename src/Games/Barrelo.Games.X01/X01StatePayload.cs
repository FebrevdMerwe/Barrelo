using Barrelo.GameSdk;

namespace Barrelo.Games.X01;

public sealed record X01StatePayload(
    IReadOnlyList<X01GroupScore> Groups,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows,
    /// <summary>
    /// The throws of the visit that just ended (3rd dart, bust, or checkout), captured the instant
    /// before CurrentVisitThrows was cleared. Rebuild() clears CurrentVisitThrows in the same pass that
    /// adds the visit-ending dart, so without this field the client would never see that dart at all.
    /// Empty unless the most recent log entry is what ended a visit.
    /// </summary>
    IReadOnlyList<DetectedThrow> JustEndedVisitThrows);

public sealed record X01GroupScore(
    int GroupIndex,
    IReadOnlyList<Guid> PlayerIds,
    int RemainingScore,
    int LegsWon,
    int SetsWon);
