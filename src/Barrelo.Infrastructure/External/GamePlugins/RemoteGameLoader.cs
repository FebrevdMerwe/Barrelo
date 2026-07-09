using System.Text.Json;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Scans pluginsDirectory for plugin.json manifests (recursively, mirroring how PluginGameLoader
/// scans for *.dll), building one RemoteGameFactory per manifest. A game is never spawned here — only its
/// static descriptor is read — so listing every installed remote game costs nothing at startup.</summary>
public sealed class RemoteGameLoader(ILoggerFactory loggerFactory)
{
    public const int SupportedProtocolVersion = 1;

    private readonly ILogger _logger = loggerFactory.CreateLogger<RemoteGameLoader>();

    public IReadOnlyList<IGameFactory> LoadFactories(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
            return [];

        var factories = new List<IGameFactory>();

        foreach (var manifestPath in Directory.EnumerateFiles(pluginsDirectory, "plugin.json", SearchOption.AllDirectories))
        {
            RemoteGameManifest? manifest;
            try
            {
                var json = File.ReadAllText(manifestPath);
                manifest = JsonSerializer.Deserialize<RemoteGameManifest>(json, RemoteGameJsonOptions.Default);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse plugin manifest '{Path}'; skipping.", manifestPath);
                continue;
            }

            if (manifest is null || string.IsNullOrWhiteSpace(manifest.GameId))
            {
                _logger.LogWarning("Plugin manifest '{Path}' is missing a gameId; skipping.", manifestPath);
                continue;
            }

            if (manifest.ProtocolVersion != SupportedProtocolVersion)
            {
                _logger.LogWarning(
                    "Plugin manifest '{Path}' declares protocolVersion {Version}, but this host supports {Supported}; skipping game '{GameId}'.",
                    manifestPath, manifest.ProtocolVersion, SupportedProtocolVersion, manifest.GameId);
                continue;
            }

            var pluginDirectory = Path.GetDirectoryName(manifestPath)!;
            var folderName = Path.GetFileName(pluginDirectory);
            if (!string.Equals(folderName, manifest.GameId, StringComparison.Ordinal))
            {
                // The RPC/spawn machinery uses pluginDirectory directly, so the game still runs fine — but
                // the shell fetches UI assets from /plugins/{gameId}/... (matching the manifest's declared
                // gameId, not the folder's actual name), so a mismatch here silently 404s ui/index.html and
                // render.js while the game itself keeps working. This is exactly that easy-to-miss case.
                _logger.LogWarning(
                    "Plugin manifest '{Path}' declares gameId '{GameId}' but lives in a folder named '{FolderName}' — " +
                    "rename the folder to match the gameId, or its ui/index.html and render.js will 404 even though the game itself works.",
                    manifestPath, manifest.GameId, folderName);
            }

            var factoryLogger = loggerFactory.CreateLogger($"RemoteGame.{manifest.GameId}");
            factories.Add(new RemoteGameFactory(manifest, pluginDirectory, factoryLogger));
            _logger.LogInformation("Loaded remote game plugin '{GameId}' from '{Path}'.", manifest.GameId, manifestPath);
        }

        return factories;
    }
}
