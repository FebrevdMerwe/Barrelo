using Darts.GameSdk;

namespace Darts.Games.X01;

public sealed record X01StatePayload(
    IReadOnlyList<X01PlayerScore> Players,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows);

public sealed record X01PlayerScore(Guid PlayerId, int RemainingScore, int LegsWon, int SetsWon);
