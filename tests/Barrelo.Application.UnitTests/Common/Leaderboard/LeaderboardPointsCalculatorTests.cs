using Barrelo.Application.Common.Leaderboard;
using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Application.UnitTests.Common.Leaderboard;

public class LeaderboardPointsCalculatorTests
{
    [Fact]
    public void Four_solo_players_get_3_2_1_0()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();
        var p4 = Guid.NewGuid();
        var result = new GameResult([p1], [p1, p2, p3, p4]);
        var playerGroups = new Dictionary<Guid, int> { [p1] = 0, [p2] = 1, [p3] = 2, [p4] = 3 };

        var awards = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        awards.Should().BeEquivalentTo(new Dictionary<Guid, int> { [p1] = 3, [p2] = 2, [p3] = 1 });
    }

    [Fact]
    public void Two_player_team_tying_for_first_both_get_the_full_placement_points()
    {
        var winnerA = Guid.NewGuid();
        var winnerB = Guid.NewGuid();
        var loser = Guid.NewGuid();
        var result = new GameResult([winnerA, winnerB], [winnerA, winnerB, loser]);
        var playerGroups = new Dictionary<Guid, int> { [winnerA] = 0, [winnerB] = 0, [loser] = 1 };

        var awards = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        awards.Should().BeEquivalentTo(new Dictionary<Guid, int> { [winnerA] = 3, [winnerB] = 3, [loser] = 2 });
    }

    [Fact]
    public void Uneven_group_sizes_still_rank_by_group_not_by_position()
    {
        var firstA = Guid.NewGuid();
        var firstB = Guid.NewGuid();
        var second = Guid.NewGuid();
        var thirdA = Guid.NewGuid();
        var thirdB = Guid.NewGuid();
        var result = new GameResult([firstA, firstB], [firstA, firstB, second, thirdA, thirdB]);
        var playerGroups = new Dictionary<Guid, int>
        {
            [firstA] = 0, [firstB] = 0,
            [second] = 1,
            [thirdA] = 2, [thirdB] = 2,
        };

        var awards = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        awards.Should().BeEquivalentTo(new Dictionary<Guid, int>
        {
            [firstA] = 3, [firstB] = 3,
            [second] = 2,
            [thirdA] = 1, [thirdB] = 1,
        });
    }

    [Fact]
    public void Placements_past_third_score_zero_and_are_omitted()
    {
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var result = new GameResult([ids[0]], ids);
        var playerGroups = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);

        var awards = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        awards.Should().BeEquivalentTo(new Dictionary<Guid, int> { [ids[0]] = 3, [ids[1]] = 2, [ids[2]] = 1 });
    }

    [Fact]
    public void Single_player_group_gets_first_place_points()
    {
        var solo = Guid.NewGuid();
        var result = new GameResult([solo], [solo]);
        var playerGroups = new Dictionary<Guid, int> { [solo] = 0 };

        var awards = LeaderboardPointsCalculator.ComputePointsAwarded(result, playerGroups);

        awards.Should().BeEquivalentTo(new Dictionary<Guid, int> { [solo] = 3 });
    }
}
