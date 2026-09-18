using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Common.Interfaces.Services;

/// <summary>Installs/removes client-owned game plugins at runtime — the only kind that can change without a
/// restart, since a built-in .NET plugin is loaded once into a collectible AssemblyLoadContext at startup.</summary>
public interface IGameInstaller
{
    /// <summary>Extracts a game package (a zip containing plugin.json at its root, or in a single top-level
    /// folder) into the plugins directory and makes it immediately playable. Installing over an existing
    /// client-owned gameId replaces it; installing over a built-in gameId is rejected.</summary>
    Task<ErrorOr<GameDescriptor>> Install(Stream packageZip, CancellationToken ct);

    /// <summary>Deletes a client-owned game's plugin directory. Rejected for a built-in gameId or an unknown
    /// one.</summary>
    Task<ErrorOr<Deleted>> Remove(string gameId, CancellationToken ct);
}
