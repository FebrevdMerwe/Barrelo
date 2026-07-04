using Barrelo.GameSdk;

namespace Barrelo.Games.X01;

public sealed record X01StatePayload(
    IReadOnlyList<X01GroupScore> Groups,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows);

public sealed record X01GroupScore(
    int GroupIndex,
    IReadOnlyList<Guid> PlayerIds,
    int RemainingScore,
    int LegsWon,
    int SetsWon);
