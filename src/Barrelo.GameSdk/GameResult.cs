namespace Barrelo.GameSdk;

/// <summary><see cref="FinalStandings"/> is ids ordered by group rank, in participant order within each
/// group, for team games.</summary>
public sealed record GameResult(IReadOnlyList<Guid> WinnerPlayerIds, IReadOnlyList<Guid> FinalStandings);
