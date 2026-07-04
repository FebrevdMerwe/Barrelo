using System.Net;
using System.Net.Http.Json;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Api.IntegrationTests;

/// <summary>
/// Own IClassFixture instance (a fresh WebApplicationFactory, and thus a fresh singleton
/// GameSessionManager) — deliberately not sharing a class with ManualFullLegEndToEndTests/
/// GamesAndMatchesEndpointsTests, which each start a match and never complete it.
/// </summary>
public class SingleActiveMatchTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>
{
    [Fact]
    public async Task Starting_a_second_match_while_one_is_active_evicts_the_first_and_succeeds()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("P1", "P2");
        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        var firstResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var first = await firstResponse.Content.ReadFromJsonAsync<StartMatchResult>(JsonTestOptions.Options);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = await secondResponse.Content.ReadFromJsonAsync<StartMatchResult>(JsonTestOptions.Options);

        // The evicted first match's snapshot is still readable, frozen at its last known state.
        var firstStateResponse = await client.GetAsync($"/api/matches/{first!.MatchId}");
        firstStateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // A manual throw now targets the second (active) match, not the first.
        var throwResponse = await client.PostAsJsonAsync(
            "/api/detection/manual-throw",
            new ManualThrowRequest(20, Ring.Outer),
            JsonTestOptions.Options);
        throwResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterThrow = await throwResponse.Content.ReadFromJsonAsync<GameStateSnapshot>(JsonTestOptions.Options);
        afterThrow!.MatchId.Should().Be(second!.MatchId);
    }
}
