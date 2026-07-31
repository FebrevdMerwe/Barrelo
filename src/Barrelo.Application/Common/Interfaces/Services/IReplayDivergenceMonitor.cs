namespace Barrelo.Application.Common.Interfaces.Services;

/// <summary>
/// A client-owned game's screens (the control tablet and the TV) each replay the same visit log to derive
/// their own state, which only agree if that replay is deterministic. When it isn't — a stray
/// Math.random(), a Date.now() — the screens quietly drift apart, and the symptom reaching a human is
/// "the TV looked wrong last night", which is close to undiagnosable.
///
/// So each client reports two hashes: one of the log it replayed, and one of the state it derived from
/// it. Comparisons are keyed on the log hash, because that is the only thing that pins down *which* input
/// a client was looking at. Clients observe the match at different moments, so anything coarser — a visit
/// count, say — groups together states that legitimately differ and makes this cry wolf. Same input,
/// different output is the one comparison that can only mean non-determinism.
///
/// Advisory only — never rejects a report, only logs — because the host has no way to know which client
/// is right.
/// </summary>
public interface IReplayDivergenceMonitor
{
    void Report(Guid matchId, string logHash, string stateHash);
}
