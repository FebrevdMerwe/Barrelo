using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Barrelo.Application.Queries.Games.ListInstalledGames;
using Barrelo.GameSdk;
using FluentAssertions;

namespace Barrelo.Api.IntegrationTests;

/// <summary>Exercises the games-management endpoints against the real filesystem (the same
/// Plugins:Directory BarreloApiFactory points every test at) — each test uses its own gameId and cleans its
/// folder up afterwards so it can't leak into another test.</summary>
public sealed class InstalledGameEndpointsTests(BarreloApiFactory factory) : IClassFixture<BarreloApiFactory>, IDisposable
{
    private readonly List<string> _installedGameIds = [];

    public void Dispose()
    {
        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        foreach (var gameId in _installedGameIds)
        {
            var directory = Path.Combine(pluginsDirectory, gameId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static byte[] BuildZip(string gameId, string description = "A game for tests.")
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("plugin.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write($$"""
                {
                  "protocolVersion": 2,
                  "gameId": "{{gameId}}",
                  "displayName": "API Test Game",
                  "description": "{{description}}",
                  "stateOwner": "client",
                  "settings": []
                }
                """);
        }

        return stream.ToArray();
    }

    private static HttpContent ZipFormContent(byte[] zip)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zip);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "file", "package.zip");
        return form;
    }

    [Fact]
    public async Task POST_api_games_installed_then_GET_lists_it_as_removable()
    {
        var client = factory.CreateClient();
        const string gameId = "apitest-install-list";
        _installedGameIds.Add(gameId);

        var postResponse = await client.PostAsync("/api/games/installed", ZipFormContent(BuildZip(gameId)));

        postResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var descriptor = await postResponse.Content.ReadFromJsonAsync<GameDescriptor>(JsonTestOptions.Options);
        descriptor!.GameId.Should().Be(gameId);

        var installed = await client.GetFromJsonAsync<List<InstalledGameItem>>("/api/games/installed", JsonTestOptions.Options);
        installed.Should().ContainSingle(g => g.GameId == gameId && g.Removable);
    }

    [Fact]
    public async Task DELETE_api_games_installed_removes_a_client_owned_game()
    {
        var client = factory.CreateClient();
        const string gameId = "apitest-install-remove";
        _installedGameIds.Add(gameId);
        await client.PostAsync("/api/games/installed", ZipFormContent(BuildZip(gameId)));

        var deleteResponse = await client.DeleteAsync($"/api/games/installed/{gameId}");

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var installed = await client.GetFromJsonAsync<List<InstalledGameItem>>("/api/games/installed", JsonTestOptions.Options);
        installed.Should().NotContain(g => g.GameId == gameId);
    }

    [Fact]
    public async Task DELETE_api_games_installed_rejects_a_built_in_game()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/games/installed/x01");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task POST_api_games_installed_rejects_a_package_without_plugin_json()
    {
        var client = factory.CreateClient();
        using var stream = new MemoryStream();
        using (new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            // an empty archive — no plugin.json anywhere
        }

        var response = await client.PostAsync("/api/games/installed", ZipFormContent(stream.ToArray()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
