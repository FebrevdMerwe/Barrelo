using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>Captures every formatted log message so a test can assert a warning was actually raised,
/// instead of only asserting on LoadFactories' return value.</summary>
internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    public List<string> Messages { get; } = [];

    public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

    public void AddProvider(ILoggerProvider provider) { }

    public void Dispose() { }

    private sealed class RecordingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }
}

public sealed class RemoteGameLoaderTests : IDisposable
{
    private readonly string _pluginsDirectory =
        Path.Combine(Path.GetTempPath(), "barrelo-remote-game-loader-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_pluginsDirectory))
            Directory.Delete(_pluginsDirectory, recursive: true);
    }

    private void WriteManifest(string gameId, string json)
    {
        var directory = Path.Combine(_pluginsDirectory, gameId);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), json);
    }

    [Fact]
    public void LoadFactories_with_a_missing_directory_returns_empty_without_throwing()
    {
        var loader = new RemoteGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(Path.Combine(_pluginsDirectory, "does-not-exist"));

        factories.Should().BeEmpty();
    }

    [Fact]
    public void LoadFactories_discovers_a_valid_manifest_and_describes_it_without_spawning_anything()
    {
        WriteManifest("yourgame", """
            {
              "protocolVersion": 1,
              "gameId": "yourgame",
              "displayName": "Your Game",
              "description": "A test game.",
              "settings": [],
              "launch": { "command": "node", "args": ["server.js", "--port", "{{port}}"], "cwd": "." },
              "health": { "path": "/health", "timeoutSeconds": 5 }
            }
            """);

        var loader = new RemoteGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().ContainSingle();
        var descriptor = factories[0].Describe();
        descriptor.GameId.Should().Be("yourgame");
        descriptor.DisplayName.Should().Be("Your Game");
    }

    [Fact]
    public void LoadFactories_skips_a_manifest_with_an_unsupported_protocol_version()
    {
        WriteManifest("toonew", $$"""
            {
              "protocolVersion": {{RemoteGameLoader.SupportedProtocolVersion + 1}},
              "gameId": "toonew",
              "displayName": "Too New",
              "description": "",
              "settings": [],
              "launch": { "command": "node", "args": [] },
              "health": { "path": "/health", "timeoutSeconds": 5 }
            }
            """);

        var loader = new RemoteGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
    }

    [Fact]
    public void LoadFactories_skips_malformed_json_without_throwing()
    {
        WriteManifest("broken", "{ not valid json");

        var loader = new RemoteGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
    }

    [Fact]
    public void LoadFactories_still_loads_a_game_whose_folder_name_does_not_match_its_gameId_but_warns()
    {
        // The RPC/spawn machinery uses the manifest's real folder path directly, so a mismatched folder
        // name doesn't stop the game from working — but it does mean /plugins/{gameId}/ui/index.html and
        // render.js resolve against the *declared* gameId, not the folder, so they'd silently 404. This is
        // exactly the mix-up a developer hits copying an example folder without renaming it.
        var directory = Path.Combine(_pluginsDirectory, "barrelo-remote-game-node");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "plugin.json"), """
            {
              "protocolVersion": 1,
              "gameId": "round-the-clock",
              "displayName": "Round the Clock",
              "description": "",
              "settings": [],
              "launch": { "command": "node", "args": [] },
              "health": { "path": "/health", "timeoutSeconds": 5 }
            }
            """);

        var loggerFactory = new RecordingLoggerFactory();
        var loader = new RemoteGameLoader(loggerFactory);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().ContainSingle(f => f.Describe().GameId == "round-the-clock");
        loggerFactory.Messages.Should().Contain(m =>
            m.Contains("round-the-clock") && m.Contains("barrelo-remote-game-node"));
    }

    [Fact]
    public void LoadFactories_skips_a_manifest_missing_a_gameId()
    {
        WriteManifest("noid", """
            {
              "protocolVersion": 1,
              "displayName": "No Id",
              "description": "",
              "launch": { "command": "node", "args": [] },
              "health": { "path": "/health", "timeoutSeconds": 5 }
            }
            """);

        var loader = new RemoteGameLoader(NullLoggerFactory.Instance);

        var factories = loader.LoadFactories(_pluginsDirectory);

        factories.Should().BeEmpty();
    }
}
