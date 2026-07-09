namespace Barrelo.GameSdk;

public enum GameStatus
{
    InProgress,
    Complete,

    /// <summary>The game's process/connection was lost mid-match (out-of-process games only) and cannot
    /// continue. Distinct from Complete — no winner, no leaderboard award, just a dead match.</summary>
    Aborted,
}
