using System.Net;
using System.Net.Http.Json;
using Darts.Api.Contracts;
using Darts.Application.Commands.Matches.StartMatch;
using Darts.Domain.Enums;
using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Api.IntegrationTests;

public class GamesAndMatchesEndpointsTests(DartsApiFactory factory) : IClassFixture<DartsApiFactory>
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

        var startResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, InputSource.Manual),
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
