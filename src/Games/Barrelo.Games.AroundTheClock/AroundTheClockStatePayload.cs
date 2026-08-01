using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock;

/// <summary>
/// <para><see cref="CurrentVisitThrows"/> keeps that exact name and shape deliberately: the shell's input
/// drawer reads <c>payload.currentVisitThrows</c> to fill its three dart slots for any game that isn't
/// client-owned, so renaming it would silently blank the drawer.</para>
/// <para><see cref="CurrentVisitSteps"/> is index-aligned with it and carries how many rungs each dart
/// actually earned (0 for a miss or a wrong number) — the board annotates its dart cards with it, since
/// with trebles jumping three the notation alone doesn't say what a dart was worth.</para>
/// </summary>
public sealed record AroundTheClockStatePayload(
    IReadOnlyList<AroundTheClockGroupProgress> Groups,
    int Round,
    int TotalSteps,
    IReadOnlyList<DetectedThrow> CurrentVisitThrows,
    IReadOnlyList<int> CurrentVisitSteps);

/// <summary>
/// <see cref="TargetNumber"/> and <see cref="TargetIsBull"/> are derived from <see cref="Progress"/>
/// rather than left to the client, so the ladder's shape stays a rule the plugin owns.
/// </summary>
public sealed record AroundTheClockGroupProgress(
    int GroupIndex,
    IReadOnlyList<Guid> PlayerIds,
    int Progress,
    int? TargetNumber,
    bool TargetIsBull,
    bool IsFinished);
