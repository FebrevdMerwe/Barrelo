using Darts.GameSdk;

namespace Darts.Games.Cricket;

public sealed record CricketStatePayload(
    IReadOnlyList<CricketGroupScore> Groups,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows);

/// <summary><see cref="Marks"/> is a fixed 7-element array index-aligned with
/// <see cref="CricketTargets.Numbers"/> (20,19,18,17,16,15) followed by Bull last.</summary>
public sealed record CricketGroupScore(
    int GroupIndex,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyList<int> Marks,
    int Points,
    int ClosedCount);
