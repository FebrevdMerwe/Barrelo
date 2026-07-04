using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Leaderboard;

/// <summary>
/// Fixed placement table: 1st = 3 points, 2nd = 2, 3rd = 1, 4th-or-worse = 0. Not scaled by player count.
/// Every member of a placed group gets the group's full points (no splitting).
/// </summary>
public static class LeaderboardPointsCalculator
{
    private static readonly int[] PlacementPoints = [3, 2, 1];

    /// <summary>
    /// Chunks result.FinalStandings into consecutive runs sharing the same groupIndex (per playerGroups)
    /// to recover which players tied for which placement, then awards points per run. Only returns
    /// entries for players who scored more than zero points.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> ComputePointsAwarded(
        GameResult result, IReadOnlyDictionary<Guid, int> playerGroups)
    {
        var awards = new Dictionary<Guid, int>();
        var rank = -1;
        int? lastGroupIndex = null;

        foreach (var playerId in result.FinalStandings)
        {
            var groupIndex = playerGroups[playerId];
            if (groupIndex != lastGroupIndex)
            {
                rank++;
                lastGroupIndex = groupIndex;
            }

            var points = rank < PlacementPoints.Length ? PlacementPoints[rank] : 0;
            if (points > 0)
                awards[playerId] = points;
        }

        return awards;
    }
}
