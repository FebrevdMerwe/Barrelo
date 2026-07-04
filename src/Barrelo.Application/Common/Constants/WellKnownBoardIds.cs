namespace Barrelo.Application.Common.Constants;

/// <summary>
/// Fixed self-identity values stamped onto DetectedThrow.BoardId by sources that have no real hardware
/// board id of their own. Purely descriptive/diagnostic metadata now (e.g. DetectionListenerService's
/// dropped-event log) — not used to route events to a match; there is at most one active match at a time.
/// </summary>
public static class WellKnownBoardIds
{
    /// <summary>The BoardId manual (on-screen) throw entry stamps on its DetectedThrows.</summary>
    public const string Manual = "manual";

    /// <summary>The BoardId the board simulator stamps on its DetectedThrows.</summary>
    public const string Simulator = "simulator";
}
