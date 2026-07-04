using Darts.Application.Common.Leaderboard;
using Darts.GameSdk;

namespace Darts.Application.Common.GameExecution;

/// <summary>
/// Wire shape returned to clients for match state: GameStateSnapshot's fields flattened out, plus
/// SessionLeaderboard — a host/session concept plugins have no business computing, deliberately kept out
/// of the Darts.GameSdk contract. SessionLeaderboard is populated only when IsComplete is true.
/// </summary>
public sealed record MatchStateSnapshotDto(
    Guid MatchId,
    string GameId,
    GameStatus Status,
    Guid? CurrentPlayerId,
    int LegNumber,
    int SetNumber,
    IReadOnlyList<DetectedThrow> RecentThrows,
    bool IsComplete,
    IReadOnlyList<Guid>? WinnerPlayerIds,
    object? Payload,
    IReadOnlyList<LeaderboardEntry>? SessionLeaderboard)
{
    public static MatchStateSnapshotDto From(GameStateSnapshot snapshot, IReadOnlyList<LeaderboardEntry>? sessionLeaderboard) =>
        new(snapshot.MatchId, snapshot.GameId, snapshot.Status, snapshot.CurrentPlayerId, snapshot.LegNumber,
            snapshot.SetNumber, snapshot.RecentThrows, snapshot.IsComplete, snapshot.WinnerPlayerIds,
            snapshot.Payload, sessionLeaderboard);
}
