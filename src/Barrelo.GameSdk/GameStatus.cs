namespace Barrelo.GameSdk;

public enum GameStatus
{
    InProgress,
    Complete,

    /// <summary>The match cannot continue and gave up rather than finished. Distinct from Complete — no
    /// winner, no leaderboard award, just a dead match whose session slot is freed so the next one can
    /// start.</summary>
    Aborted,
}
