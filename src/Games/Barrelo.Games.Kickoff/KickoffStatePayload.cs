using Barrelo.GameSdk;

namespace Barrelo.Games.Kickoff;

public sealed record KickoffStatePayload(
    IReadOnlyList<KickoffGroupScore> Groups,
    BallPosition Ball,
    IReadOnlyList<BallPosition> Trail,
    KickoffEvent? LastEvent,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows);

public sealed record KickoffGroupScore(
    int GroupIndex,
    IReadOnlyList<Guid> PlayerIds,
    int Goals,
    int LegsWon);

/// <summary>Normalized pitch-space coordinate, (0,0) top-left to (1,1) bottom-right, (0.5,0.5) center spot.</summary>
public sealed record BallPosition(double X, double Y);

public sealed record KickoffEvent(string Text, string Tone);
