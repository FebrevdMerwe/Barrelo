using System.Text.Json;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Scans pluginsDirectory for plugin.json manifests (recursively, mirroring how PluginGameLoader
/// scans for *.dll), building one ClientGameFactory per manifest. Nothing is instantiated here beyond the
/// manifest itself, so listing every installed game costs a file read.</summary>
public sealed class ClientGameLoader(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ClientGameLoader>();

    public IReadOnlyList<IGameFactory> LoadFactories(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
            return [];

        var factories = new List<IGameFactory>();

        foreach (var manifestPath in Directory.EnumerateFiles(pluginsDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            if (TryLoad(manifestPath) is { } factory)
                factories.Add(factory);
        }

        return factories;
    }

    private IGameFactory? TryLoad(string manifestPath)
    {
        PluginManifest? manifest;
        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, PluginManifestJsonOptions.Default);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse plugin manifest '{Path}'; skipping.", manifestPath);
            return null;
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.GameId))
        {
            _logger.LogWarning("Plugin manifest '{Path}' is missing a gameId; skipping.", manifestPath);
            return null;
        }

        if (manifest.ProtocolVersion == 1)
        {
            // Protocol 1 games shipped a rules server that Barrelo spawned as a child process. That path
            // has been removed — rules now run in the game's browser UI — so such a plugin cannot work at
            // all and is called out specifically rather than lumped in with a generic version mismatch.
            _logger.LogWarning(
                "Plugin '{GameId}' ('{Path}') uses protocolVersion 1, the removed out-of-process server protocol. " +
                "Port it to the client-owned model (rules in ui/, protocolVersion {Supported}, \"stateOwner\": \"{StateOwner}\") — " +
                "see the README's \"Adding a new game\" section. Skipping.",
                manifest.GameId, manifestPath, PluginManifest.SupportedProtocolVersion, PluginManifest.ClientStateOwner);
            return null;
        }

        if (manifest.ProtocolVersion != PluginManifest.SupportedProtocolVersion)
        {
            _logger.LogWarning(
                "Plugin manifest '{Path}' declares protocolVersion {Version}, but this host supports {Supported}; skipping game '{GameId}'.",
                manifestPath, manifest.ProtocolVersion, PluginManifest.SupportedProtocolVersion, manifest.GameId);
            return null;
        }

        if (!string.Equals(manifest.StateOwner, PluginManifest.ClientStateOwner, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Plugin manifest '{Path}' declares stateOwner '{StateOwner}', but only '{Supported}' is understood; skipping game '{GameId}'.",
                manifestPath, manifest.StateOwner, PluginManifest.ClientStateOwner, manifest.GameId);
            return null;
        }

        var pluginDirectory = Path.GetDirectoryName(manifestPath)!;
        var folderName = Path.GetFileName(pluginDirectory);
        if (!string.Equals(folderName, manifest.GameId, StringComparison.Ordinal))
        {
            // The shell fetches UI assets from /plugins/{gameId}/... (matching the manifest's declared
            // gameId, not the folder's actual name), so a mismatch here silently 404s ui/index.html while
            // the game is still listed and startable. This is exactly that easy-to-miss case.
            _logger.LogWarning(
                "Plugin manifest '{Path}' declares gameId '{GameId}' but lives in a folder named '{FolderName}' — " +
                "rename the folder to match the gameId, or its ui/index.html will 404 even though the game is still listed.",
                manifestPath, manifest.GameId, folderName);
        }

        _logger.LogInformation("Loaded client-owned game plugin '{GameId}' from '{Path}'.", manifest.GameId, manifestPath);
        return new ClientGameFactory(manifest);
    }
}
