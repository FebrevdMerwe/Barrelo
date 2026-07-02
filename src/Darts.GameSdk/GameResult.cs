namespace Darts.GameSdk;

public sealed record GameResult(Guid? WinnerPlayerId, IReadOnlyList<Guid> FinalStandings);
