using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Installs/removes client-owned game plugins under the plugins directory and keeps the shared
/// GameCatalog's client-owned entries in sync. A SemaphoreSlim serializes install/remove so two uploads (or
/// an upload racing a delete) can't interleave their filesystem writes or catalog reloads.</summary>
public sealed partial class ClientGameInstaller(
    GameCatalog catalog,
    string pluginsDirectory,
    ILoggerFactory loggerFactory)
    : IGameInstaller
{
    private static readonly Regex GameIdPattern = GameIdRegex();

    private readonly ClientGameLoader _loader = new(loggerFactory);
    private readonly ILogger<ClientGameInstaller> _logger = loggerFactory.CreateLogger<ClientGameInstaller>();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ErrorOr<GameDescriptor>> Install(Stream packageZip, CancellationToken ct)
    {
        // Staged inside pluginsDirectory itself (not the system temp folder) so the final Directory.Move
        // below is a same-volume rename rather than a cross-volume copy, which Directory.Move can't do.
        Directory.CreateDirectory(pluginsDirectory);
        var stagingDirectory = Path.Combine(pluginsDirectory, $".install-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            try
            {
                ZipFile.ExtractToDirectory(packageZip, stagingDirectory, overwriteFiles: false);
            }
            catch (InvalidDataException)
            {
                return GameErrors.InvalidPackage("The uploaded file is not a valid zip archive.");
            }

            var contentRootResult = ResolveContentRoot(stagingDirectory);
            if (contentRootResult.IsError)
                return contentRootResult.Errors;
            var contentRoot = contentRootResult.Value;

            var manifestResult = ReadManifest(Path.Combine(contentRoot, "plugin.json"));
            if (manifestResult.IsError)
                return manifestResult.Errors;
            var manifest = manifestResult.Value;

            if (!GameIdPattern.IsMatch(manifest.GameId))
            {
                return GameErrors.InvalidPackage(
                    $"gameId '{manifest.GameId}' must be lowercase kebab-case (letters, digits, single hyphens between them).");
            }

            await _gate.WaitAsync(ct);
            try
            {
                var existing = catalog.Resolve(manifest.GameId);
                if (!existing.IsError && !catalog.IsClientOwned(manifest.GameId))
                    return GameErrors.GameIdConflict(manifest.GameId);

                var destination = Path.Combine(pluginsDirectory, manifest.GameId);
                if (Directory.Exists(destination))
                    Directory.Delete(destination, recursive: true);

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                Directory.Move(contentRoot, destination);

                Reload();
                _logger.LogInformation("Installed client-owned game '{GameId}' from an uploaded package.", manifest.GameId);
                return manifest.ToDescriptor();
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
        }
    }

    public async Task<ErrorOr<Deleted>> Remove(string gameId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var existing = catalog.Resolve(gameId);
            if (existing.IsError)
                return GameErrors.GameNotFound(gameId);

            if (!catalog.IsClientOwned(gameId))
                return GameErrors.CannotRemoveBuiltIn(gameId);

            var directory = Path.Combine(pluginsDirectory, gameId);
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);

            Reload();
            _logger.LogInformation("Removed client-owned game '{GameId}'.", gameId);
            return Result.Deleted;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Reload() => catalog.ReloadClientGames(_loader.LoadFactories(pluginsDirectory));

    /// <summary>A game package is either plugin.json at the zip root, or in a single top-level folder
    /// (what you get zipping the folder itself rather than its contents) — accepting both avoids a
    /// zip-tool-specific failure mode that would otherwise be indistinguishable from a broken package.</summary>
    private static ErrorOr<string> ResolveContentRoot(string extractedRoot)
    {
        if (File.Exists(Path.Combine(extractedRoot, "plugin.json")))
            return extractedRoot;

        var topLevelDirectories = Directory.GetDirectories(extractedRoot);
        if (Directory.GetFiles(extractedRoot).Length == 0 && topLevelDirectories.Length == 1 &&
            File.Exists(Path.Combine(topLevelDirectories[0], "plugin.json")))
        {
            return topLevelDirectories[0];
        }

        return GameErrors.InvalidPackage(
            "The package must contain plugin.json at its root (or inside a single top-level folder).");
    }

    private static ErrorOr<PluginManifest> ReadManifest(string manifestPath)
    {
        PluginManifest? manifest;
        try
        {
            var json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<PluginManifest>(json, PluginManifestJsonOptions.Default);
        }
        catch (Exception ex)
        {
            return GameErrors.InvalidPackage($"plugin.json could not be parsed: {ex.Message}");
        }

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.GameId))
            return GameErrors.InvalidPackage("plugin.json is missing a gameId.");

        if (manifest.ProtocolVersion != PluginManifest.SupportedProtocolVersion)
        {
            return GameErrors.InvalidPackage(
                $"plugin.json declares protocolVersion {manifest.ProtocolVersion}, but this host supports " +
                $"{PluginManifest.SupportedProtocolVersion}.");
        }

        if (!string.Equals(manifest.StateOwner, PluginManifest.ClientStateOwner, StringComparison.Ordinal))
        {
            return GameErrors.InvalidPackage(
                $"plugin.json declares stateOwner '{manifest.StateOwner}', but only '{PluginManifest.ClientStateOwner}' " +
                "can be installed here.");
        }

        return manifest;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex GameIdRegex();
}
