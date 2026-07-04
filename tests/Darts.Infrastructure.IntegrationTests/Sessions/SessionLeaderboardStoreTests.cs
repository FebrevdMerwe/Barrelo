using Darts.Application.Common.Leaderboard;
using Darts.Infrastructure.External.Sessions;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.Sessions;

public class SessionLeaderboardStoreTests
{
    [Fact]
    public void RecordResult_accumulates_points_for_the_same_player_across_matches()
    {
        var store = new SessionLeaderboardStore();
        var playerId = Guid.NewGuid();

        store.RecordResult(Guid.NewGuid(), [new LeaderboardEntry(playerId, "Sam", 3)]);
        store.RecordResult(Guid.NewGuid(), [new LeaderboardEntry(playerId, "Sam", 2)]);

        store.GetStandings().Should().ContainSingle(e => e.PlayerId == playerId && e.Points == 5);
    }

    [Fact]
    public void RecordResult_for_an_already_recorded_matchId_is_a_no_op()
    {
        var store = new SessionLeaderboardStore();
        var playerId = Guid.NewGuid();
        var matchId = Guid.NewGuid();

        store.RecordResult(matchId, [new LeaderboardEntry(playerId, "Sam", 3)]);
        store.RecordResult(matchId, [new LeaderboardEntry(playerId, "Sam", 3)]);

        store.GetStandings().Should().ContainSingle(e => e.PlayerId == playerId && e.Points == 3);
    }

    [Fact]
    public void GetStandings_orders_by_points_descending_then_name_ascending()
    {
        var store = new SessionLeaderboardStore();
        var alex = Guid.NewGuid();
        var jo = Guid.NewGuid();
        var sam = Guid.NewGuid();

        store.RecordResult(Guid.NewGuid(),
            [new LeaderboardEntry(sam, "Sam", 2), new LeaderboardEntry(alex, "Alex", 3), new LeaderboardEntry(jo, "Jo", 2)]);

        store.GetStandings().Select(e => e.PlayerId).Should().Equal(alex, jo, sam);
    }

    [Fact]
    public void Reset_clears_all_totals()
    {
        var store = new SessionLeaderboardStore();
        store.RecordResult(Guid.NewGuid(), [new LeaderboardEntry(Guid.NewGuid(), "Sam", 3)]);

        store.Reset();

        store.GetStandings().Should().BeEmpty();
    }
}
