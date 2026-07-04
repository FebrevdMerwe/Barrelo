using System.Net;
using System.Net.Http.Json;
using Barrelo.Api.Contracts;
using FluentAssertions;

namespace Barrelo.Api.IntegrationTests;

public class PlayerEndpointsTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>
{
    // Player has no parameterless/JsonConstructor, so client-side deserialization uses this
    // structurally-matching local record instead of the real domain entity.
    private sealed record PlayerDto(Guid Id, string Name, DateTimeOffset CreatedAtUtc, bool IsPermanent, bool IsBenched);

    [Fact]
    public async Task DELETE_api_players_removes_a_session_scoped_player()
    {
        var client = factory.CreateClient();
        var created = await CreateSessionPlayer(client, "Nadia Osei");

        var deleteResponse = await client.DeleteAsync($"/api/players/{created.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var players = await client.GetFromJsonAsync<List<PlayerDto>>("/api/players", JsonTestOptions.Options);
        players.Should().NotContain(p => p.Id == created.Id);
    }

    [Fact]
    public async Task DELETE_api_players_for_unknown_id_returns_not_found()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync($"/api/players/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_api_players_for_a_permanent_player_benches_it_instead_of_deleting()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("Deshawn Ruiz");

        var deleteResponse = await client.DeleteAsync($"/api/players/{playerIds[0]}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var players = await client.GetFromJsonAsync<List<PlayerDto>>("/api/players", JsonTestOptions.Options);
        var benched = players.Should().ContainSingle(p => p.Id == playerIds[0]).Subject;
        benched.IsPermanent.Should().BeTrue();
        benched.IsBenched.Should().BeTrue();
    }

    [Fact]
    public async Task POST_unbench_reverses_a_bench()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("Priya Anand");
        await client.DeleteAsync($"/api/players/{playerIds[0]}");

        var unbenchResponse = await client.PostAsync($"/api/players/{playerIds[0]}/unbench", content: null);
        unbenchResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var players = await client.GetFromJsonAsync<List<PlayerDto>>("/api/players", JsonTestOptions.Options);
        players.Should().ContainSingle(p => p.Id == playerIds[0]).Which.IsBenched.Should().BeFalse();
    }

    [Fact]
    public async Task DELETE_permanent_removes_the_player_even_with_active_match_history()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("Amara Diallo", "Owen Clarke");
        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var startResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest("x01", playerIds, null, playerGroups),
            JsonTestOptions.Options);
        startResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var deleteResponse = await client.DeleteAsync($"/api/players/{playerIds[0]}/permanent");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var players = await client.GetFromJsonAsync<List<PlayerDto>>("/api/players", JsonTestOptions.Options);
        players.Should().NotContain(p => p.Id == playerIds[0]);
    }

    [Fact]
    public async Task POST_restore_reinserts_an_erased_session_player_under_the_same_id()
    {
        var client = factory.CreateClient();
        var created = await CreateSessionPlayer(client, "Tomas Novak");
        await client.DeleteAsync($"/api/players/{created.Id}");

        var restoreResponse = await client.PostAsJsonAsync(
            $"/api/players/{created.Id}/restore",
            new RestorePlayerRequest(created.Name, created.CreatedAtUtc),
            JsonTestOptions.Options);

        restoreResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var players = await client.GetFromJsonAsync<List<PlayerDto>>("/api/players", JsonTestOptions.Options);
        var restored = players.Should().ContainSingle(p => p.Id == created.Id).Subject;
        restored.Name.Should().Be(created.Name);
        restored.IsPermanent.Should().BeFalse();
    }

    private static async Task<PlayerDto> CreateSessionPlayer(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/players/session", new CreatePlayerRequest(name), JsonTestOptions.Options);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<PlayerDto>(JsonTestOptions.Options))!;
    }
}
