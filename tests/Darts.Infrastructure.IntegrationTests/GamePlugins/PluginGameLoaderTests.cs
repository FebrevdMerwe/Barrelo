using Darts.GameSdk;
using Darts.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Darts.Infrastructure.IntegrationTests.GamePlugins;

public class PluginGameLoaderTests
{
    private static string PluginsDirectory => Path.Combine(AppContext.BaseDirectory, "plugins");

    [Fact]
    public void LoadFactories_discovers_the_x01_plugin_from_the_real_plugins_folder()
    {
        var loader = new PluginGameLoader(NullLogger<PluginGameLoader>.Instance);

        var factories = loader.LoadFactories(PluginsDirectory);

        factories.Should().ContainSingle(f => f.Describe().GameId == "x01");
    }

    [Fact]
    public void Loaded_plugin_assembly_references_the_exact_same_GameSdk_assembly_as_the_host()
    {
        var loader = new PluginGameLoader(NullLogger<PluginGameLoader>.Instance);
        var factory = loader.LoadFactories(PluginsDirectory).Single(f => f.Describe().GameId == "x01");

        var pluginGameSdkReference = factory.GetType().Assembly
            .GetReferencedAssemblies()
            .Single(a => a.Name == "Darts.GameSdk");
        var hostGameSdkAssembly = typeof(IGameFactory).Assembly.GetName();

        pluginGameSdkReference.FullName.Should().Be(hostGameSdkAssembly.FullName);
    }

    [Fact]
    public async Task Loaded_plugin_produces_an_IGame_whose_state_casts_cleanly_to_the_hosts_GameSdk_types()
    {
        // If PluginLoadContext failed to defer Darts.GameSdk resolution to the host's copy, the plugin
        // would materialize a second, distinct GameStateSnapshot type and this call would throw
        // InvalidCastException/TargetInvocationException the moment the ALC boundary is crossed.
        var loader = new PluginGameLoader(NullLogger<PluginGameLoader>.Instance);
        var factory = loader.LoadFactories(PluginsDirectory).Single(f => f.Describe().GameId == "x01");

        var game = await factory.Create(new GameSetup([Guid.NewGuid()], new Dictionary<string, string>()), CancellationToken.None);
        var state = await game.GetState();

        state.Should().BeOfType<GameStateSnapshot>();
        state.GameId.Should().Be("x01");
    }

    [Fact]
    public void LoadFactories_with_a_missing_directory_returns_empty_without_throwing()
    {
        var loader = new PluginGameLoader(NullLogger<PluginGameLoader>.Instance);

        var factories = loader.LoadFactories(Path.Combine(AppContext.BaseDirectory, "no-such-plugins-dir"));

        factories.Should().BeEmpty();
    }
}
