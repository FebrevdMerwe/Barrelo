namespace Barrelo.Application.Common.Leaderboard;

/// <summary>
/// A player's cumulative session points. PlayerName is a snapshot taken at the moment points were
/// recorded — never a live roster lookup — so an entry survives the player being erased or permanently
/// deleted mid-session.
/// </summary>
public sealed record LeaderboardEntry(Guid PlayerId, string PlayerName, int Points);
