namespace Darts.GameSdk;

/// <summary>Universal state envelope. Game-specific shape lives entirely in <see cref="Payload"/> — never add game-specific fields here.</summary>
public sealed record GameStateSnapshot(
    Guid MatchId,
    string GameId,
    GameStatus Status,
    Guid? CurrentPlayerId,
    int LegNumber,
    int SetNumber,
    IReadOnlyList<DetectedThrow> RecentThrows,
    bool IsComplete,
    Guid? WinnerPlayerId,
    object? Payload);
