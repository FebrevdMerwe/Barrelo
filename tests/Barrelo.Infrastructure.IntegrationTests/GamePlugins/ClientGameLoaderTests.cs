using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

public sealed class ClientGameLoaderTests : IDisposable
{
    private readonly string _pluginsDirectory =
        Path.Combine(Path.GetTempPath(), "barrelo-client-game-loader-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_pluginsDirectory))
            Directory.Delete(_pluginsDirectory, recursive: true);
    }

    private void WriteManifest(string folderName, string json)
    {
        var directory = Path.Combine(_pluginsDirectory, folderName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), json);
    }

    private static string ValidManifest(string gameId) =>
        $$"""
        {
          "protocolVersion": 2,
          "gameId": "{{gameId}}",
          "displayName": "Test Game",
          "description": "A game for tests.",
          "stateOwner": "client",
          "settings": []
        }
        """;

    [Fact]
    public void LoadFactories_with_a_missing_directory_returns_empty_without_throwing()
    {
        var loader = new ClientGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(Path.Combine(_pluginsDirectory, "does-not-exist"));

        factories.Should().BeEmpty();
    }

    [Fact]
    public void LoadFactories_reads_a_valid_manifest_into_a_descriptor_without_starting_anything()
    {
        WriteManifest("testgame", ValidManifest("testgame"));
        var loader = new ClientGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().ContainSingle();
        var descriptor = factories[0].Describe();
        descriptor.GameId.Should().Be("testgame");
        descriptor.DisplayName.Should().Be("Test Game");
        descriptor.Description.Should().Be("A game for tests.");
    }

    [Fact]
    public void LoadFactories_skips_a_protocol_v1_manifest_and_explains_that_the_server_path_is_gone()
    {
        WriteManifest("legacy",
            """
            {
              "protocolVersion": 1,
              "gameId": "legacy",
              "displayName": "Legacy Game",
              "description": "Shipped its own rules server.",
              "launch": { "command": "node", "args": ["server.js"] },
              "settings": []
            }
            """);
        var loggerFactory = new RecordingLoggerFactory();

        var factories = new ClientGameLoader(loggerFactory).LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
        // A v1 plugin can't work at all now, so the warning has to say what to do about it — not just
        // that a number didn't match.
        loggerFactory.Messages.Should().ContainSingle(m =>
            m.Contains("legacy") && m.Contains("protocolVersion 1") && m.Contains("client-owned"));
    }

    [Fact]
    public void LoadFactories_skips_a_manifest_declaring_an_unsupported_protocol_version()
    {
        WriteManifest("future", ValidManifest("future").Replace("\"protocolVersion\": 2", "\"protocolVersion\": 99"));
        var loggerFactory = new RecordingLoggerFactory();

        var factories = new ClientGameLoader(loggerFactory).LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
        loggerFactory.Messages.Should().ContainSingle(m => m.Contains("future") && m.Contains("99"));
    }

    [Fact]
    public void LoadFactories_skips_a_manifest_that_does_not_declare_a_client_state_owner()
    {
        WriteManifest("hosted", ValidManifest("hosted").Replace("\"stateOwner\": \"client\"", "\"stateOwner\": \"server\""));
        var loggerFactory = new RecordingLoggerFactory();

        var factories = new ClientGameLoader(loggerFactory).LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
        loggerFactory.Messages.Should().ContainSingle(m => m.Contains("hosted") && m.Contains("server"));
    }

    [Fact]
    public void LoadFactories_skips_a_malformed_manifest_without_failing_the_whole_scan()
    {
        WriteManifest("broken", "{ this is not json");
        WriteManifest("healthy", ValidManifest("healthy"));
        var loader = new ClientGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().ContainSingle();
        factories[0].Describe().GameId.Should().Be("healthy");
    }

    [Fact]
    public void LoadFactories_skips_a_manifest_with_no_gameId()
    {
        WriteManifest("anonymous", ValidManifest("").Replace("\"gameId\": \"\",", ""));
        var loader = new ClientGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
    }

    [Fact]
    public void LoadFactories_loads_a_gameId_folder_mismatch_but_warns_that_its_ui_will_404()
    {
        WriteManifest("wrong-folder-name", ValidManifest("testgame"));
        var loggerFactory = new RecordingLoggerFactory();

        var factories = new ClientGameLoader(loggerFactory).LoadFactories(_pluginsDirectory);

        // The game is still listed and startable — only its UI assets, fetched from /plugins/{gameId}/,
        // would silently 404. That's exactly why this warns rather than skipping.
        factories.Should().ContainSingle();
        // Matched on the advice rather than just the two names — the "loaded plugin" info line also
        // mentions both, since the logged path contains the folder name.
        loggerFactory.Messages.Should().ContainSingle(m =>
            m.Contains("rename the folder") && m.Contains("testgame") && m.Contains("wrong-folder-name"));
    }
}
