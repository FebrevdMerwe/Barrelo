using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Deserialized shape of a client-owned game's plugin.json. The descriptor fields (GameId,
/// DisplayName, Description, Settings) mirror GameDescriptor exactly — no separate schema, since
/// GameDescriptor's polymorphic Settings already round-trip through System.Text.Json.
///
/// There is deliberately no launch/health spec: a game's rules run in the browser, so the host never
/// spawns anything. All Barrelo reads from this file is how to list the game and how to shape its
/// start-match form.</summary>
public sealed class PluginManifest
{
    /// <summary>The only value this host understands. Declared per-manifest so an incompatible plugin is
    /// skipped with a clear log line rather than half-loaded.</summary>
    public const int SupportedProtocolVersion = 2;

    /// <summary>Marks a plugin as client-owned (rules run in the browser). The only supported value today;
    /// a manifest declaring anything else is skipped.</summary>
    public const string ClientStateOwner = "client";

    public int ProtocolVersion { get; set; }

    public string GameId { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string Description { get; set; } = "";

    public string StateOwner { get; set; } = "";

    public List<GameSettingDefinition> Settings { get; set; } = [];

    public GameDescriptor ToDescriptor() => new(GameId, DisplayName, Description, Settings);
}
