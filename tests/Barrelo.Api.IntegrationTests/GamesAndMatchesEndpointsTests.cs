using System.Net;
using System.Net.Http.Json;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Api.IntegrationTests;

public class GamesAndMatchesEndpointsTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>
{
    [Fact]
    public async Task GET_api_games_lists_the_x01_plugin()
    {
        var client = factory.CreateClient();

        var games = await client.GetFromJsonAsync<List<GameDescriptor>>("/api/games", JsonTestOptions.Options);

        games.Should().ContainSingle(g => g.GameId == "x01");
    }

    [Fact]
    public async Task POST_api_matches_then_GET_by_id_round_trips_the_match()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("P1", "P2");

        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var startResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var started = await startResponse.Content.ReadFromJsonAsync<StartMatchResult>(JsonTestOptions.Options);
        started.Should().NotBeNull();
        started!.InitialState.GameId.Should().Be("x01");

        var getResponse = await client.GetAsync($"/api/matches/{started.MatchId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var state = await getResponse.Content.ReadFromJsonAsync<GameStateSnapshot>(JsonTestOptions.Options);
        state!.MatchId.Should().Be(started.MatchId);
        state.IsComplete.Should().BeFalse();
    }

    [Fact]
    public async Task GET_api_matches_for_unknown_id_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/matches/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
