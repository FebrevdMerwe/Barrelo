using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>
/// Covers RemoteGameFactory's spawn/health-check failure path using a real (but non-HTTP-serving) child
/// process, so the process-spawn/kill machinery is genuinely exercised without needing an external
/// runtime — the happy path (spawn -> health-check success -> /create) is covered manually against the
/// real Node reference example instead (examples/barrelo-remote-game-node/README.md's checklist), per the
/// project's "keep dotnet test pure-.NET" gate.
/// </summary>
public sealed class RemoteGameFactoryTests
{
    private static RemoteGameManifest ManifestLaunchingANonHttpProcess() => new()
    {
        ProtocolVersion = RemoteGameLoader.SupportedProtocolVersion,
        GameId = "never-answers",
        DisplayName = "Never Answers",
        Description = "A process that starts but never serves HTTP, for health-check-timeout tests.",
        Launch = OperatingSystem.IsWindows()
            ? new RemoteGameManifest.LaunchSpec { Command = "ping", Args = ["-n", "30", "127.0.0.1"] }
            : new RemoteGameManifest.LaunchSpec { Command = "sleep", Args = ["30"] },
        Health = new RemoteGameManifest.HealthSpec { Path = "/health", TimeoutSeconds = 1 },
    };

    [Fact]
    public async Task Create_throws_GameUnavailableException_when_the_process_never_becomes_healthy()
    {
        var factory = new RemoteGameFactory(
            ManifestLaunchingANonHttpProcess(),
            pluginDirectory: AppContext.BaseDirectory,
            NullLogger.Instance);

        var act = () => factory.Create(new GameSetup([Guid.NewGuid()], new Dictionary<string, string>()), CancellationToken.None);

        await act.Should().ThrowAsync<GameUnavailableException>();
    }

    [Fact]
    public void Describe_returns_the_manifest_descriptor_without_spawning_a_process()
    {
        var manifest = ManifestLaunchingANonHttpProcess();
        var factory = new RemoteGameFactory(manifest, AppContext.BaseDirectory, NullLogger.Instance);

        var descriptor = factory.Describe();

        descriptor.GameId.Should().Be(manifest.GameId);
        descriptor.DisplayName.Should().Be(manifest.DisplayName);
    }
}
