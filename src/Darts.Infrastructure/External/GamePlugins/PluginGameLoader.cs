using Darts.GameSdk;
using Microsoft.Extensions.Logging;

namespace Darts.Infrastructure.External.GamePlugins;

public sealed class PluginGameLoader(ILogger<PluginGameLoader> logger)
{
    /// <summary>Scans pluginsDirectory (an absolute path) for *.dll, loading each into its own collectible ALC.</summary>
    public IReadOnlyList<IGameFactory> LoadFactories(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            logger.LogInformation("Plugins directory '{Directory}' does not exist; no plugins loaded.", pluginsDirectory);
            return [];
        }

        var factories = new List<IGameFactory>();

        foreach (var dllPath in Directory.EnumerateFiles(pluginsDirectory, "*.dll", SearchOption.AllDirectories))
        {
            var context = new PluginLoadContext(dllPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || !typeof(IGameFactory).IsAssignableFrom(type))
                    continue;

                if (Activator.CreateInstance(type) is IGameFactory factory)
                {
                    factories.Add(factory);
                    logger.LogInformation("Loaded game plugin '{GameId}' from '{DllPath}'.", factory.Describe().GameId, dllPath);
                }
            }
        }

        return factories;
    }
}
