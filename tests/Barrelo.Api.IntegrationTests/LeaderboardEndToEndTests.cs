using System.Net.Http.Json;
using Barrelo.Api.Contracts;
using Barrelo.Application.Common.GameExecution;
using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Api.IntegrationTests;

/// <summary>
/// Covers the session leaderboard end to end over real HTTP: a completing match's response carries
/// standings, re-fetching the completed match afterwards still shows them, and resetting clears them.
/// </summary>
public class LeaderboardEndToEndTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>
{
    [Fact]
    public async Task Completing_a_match_awards_points_that_survive_a_refresh_and_clear_on_reset()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("Winner", "Runner Up");
        var winnerId = playerIds[0];
        var runnerUpId = playerIds[1];

        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var startResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest(
                "x01",
                playerIds,
                new Dictionary<string, string> { ["startingScore"] = "40", ["legsToWin"] = "1", ["setsToWin"] = "1" },
                playerGroups),
            JsonTestOptions.Options);
        startResponse.EnsureSuccessStatusCode();
        var matchId = (await startResponse.Content.ReadFromJsonAsync<StartMatchResponse>(JsonTestOptions.Options))!.MatchId;

        // Double-20 checkout from a 40 start wins the match in a single throw.
        var throwResponse = await client.PostAsJsonAsync(
            "/api/detection/manual-throw", new ManualThrowRequest(20, Ring.Double), JsonTestOptions.Options);
        throwResponse.EnsureSuccessStatusCode();
        var completedState = (await throwResponse.Content.ReadFromJsonAsync<MatchStateSnapshotDto>(JsonTestOptions.Options))!;

        completedState.IsComplete.Should().BeTrue();
        completedState.WinnerPlayerIds.Should().BeEquivalentTo([winnerId]);
        completedState.SessionLeaderboard.Should().NotBeNull();
        completedState.SessionLeaderboard!.Should().Contain(e => e.PlayerId == winnerId && e.Points == 3);
        completedState.SessionLeaderboard!.Should().Contain(e => e.PlayerId == runnerUpId && e.Points == 2);

        var refetchResponse = await client.GetAsync($"/api/matches/{matchId}");
        refetchResponse.EnsureSuccessStatusCode();
        var refetchedState = (await refetchResponse.Content.ReadFromJsonAsync<MatchStateSnapshotDto>(JsonTestOptions.Options))!;
        refetchedState.SessionLeaderboard.Should().Contain(e => e.PlayerId == winnerId && e.Points == 3);

        var resetResponse = await client.PostAsync("/api/leaderboard/reset", null);
        resetResponse.EnsureSuccessStatusCode();

        var afterResetResponse = await client.GetAsync($"/api/matches/{matchId}");
        afterResetResponse.EnsureSuccessStatusCode();
        var afterResetState = (await afterResetResponse.Content.ReadFromJsonAsync<MatchStateSnapshotDto>(JsonTestOptions.Options))!;
        afterResetState.SessionLeaderboard.Should().BeEmpty();
    }

    private sealed record StartMatchResponse(Guid MatchId, GameStateSnapshot InitialState);
}
