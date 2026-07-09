using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Deserialized shape of a remote game's plugin.json. The descriptor fields (GameId, DisplayName,
/// Description, Settings) mirror GameDescriptor exactly — no separate schema, since GameDescriptor's
/// polymorphic Settings already round-trip through System.Text.Json.</summary>
public sealed class RemoteGameManifest
{
    public int ProtocolVersion { get; set; }

    public string GameId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public List<GameSettingDefinition> Settings { get; set; } = [];

    public LaunchSpec Launch { get; set; } = new();

    public HealthSpec Health { get; set; } = new();

    public GameDescriptor ToDescriptor() => new(GameId, DisplayName, Description, Settings);

    public sealed class LaunchSpec
    {
        public string Command { get; set; } = "";

        public List<string> Args { get; set; } = [];

        /// <summary>Relative to the manifest's own directory; null/empty means the manifest's directory itself.</summary>
        public string? Cwd { get; set; }
    }

    public sealed class HealthSpec
    {
        public string Path { get; set; } = "/health";

        public int TimeoutSeconds { get; set; } = 5;
    }
}
