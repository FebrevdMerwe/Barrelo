using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Darts.Api.Contracts;
using Darts.Application.Commands.Matches.StartMatch;
using FluentAssertions;

namespace Darts.Api.IntegrationTests;

/// <summary>
/// Own IClassFixture instance (a fresh WebApplicationFactory, and thus a fresh singleton
/// GameSessionManager) — deliberately not sharing a class with ManualFullLegEndToEndTests/
/// GamesAndMatchesEndpointsTests, which each start a match and never complete it.
/// </summary>
public class SingleActiveMatchTests(DartsApiFactory factory) : IClassFixture<DartsApiFactory>
{
    [Fact]
    public async Task Starting_a_second_match_while_one_is_active_is_rejected_with_conflict()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("P1", "P2");
        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);

        var firstResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);

        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await secondResponse.Content.ReadFromJsonAsync<JsonElement>(JsonTestOptions.Options);
        var errorCode = problem.GetProperty("errors")[0].GetProperty("code").GetString();
        errorCode.Should().Be("Match.AlreadyActive");
    }
}
