using System.Reflection;
using System.Runtime.Loader;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>
/// Collectible ALC hosting a single plugin DLL. Types crossing the boundary (DetectedThrow, IGame, etc.)
/// must resolve to the same assembly instance on both sides, or identical-looking types throw
/// InvalidCastException — so any assembly already loaded in the default context (most importantly
/// Barrelo.GameSdk) is deferred back to the host's copy instead of being loaded a second time here.
/// </summary>
public sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var alreadyLoadedInDefault = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (alreadyLoadedInDefault is not null)
            return null;

        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        return resolvedPath is not null ? LoadFromAssemblyPath(resolvedPath) : null;
    }
}
