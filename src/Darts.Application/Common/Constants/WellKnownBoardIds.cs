namespace Darts.Application.Common.Constants;

public static class WellKnownBoardIds
{
    /// <summary>The BoardId a fully-manual match (no detector) is bound to at start.</summary>
    public const string Manual = "manual";

    /// <summary>The BoardId a Board-sourced match is bound to at start — one board, one active match (v1 rule).</summary>
    public const string Simulator = "simulator";
}
