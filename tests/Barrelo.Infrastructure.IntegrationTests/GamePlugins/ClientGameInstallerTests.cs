using System.IO.Compression;
using System.Text;
using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

public sealed class ClientGameInstallerTests : IDisposable
{
    private readonly string _pluginsDirectory =
        Path.Combine(Path.GetTempPath(), "barrelo-client-game-installer-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_pluginsDirectory))
            Directory.Delete(_pluginsDirectory, recursive: true);
    }

    private sealed class FakeGameFactory(string gameId) : IGameFactory
    {
        public GameDescriptor Describe() => new(gameId, gameId, "test", []);

        public Task<IGame> Create(GameSetup setup, CancellationToken ct) => throw new NotSupportedException();
    }

    private static string ValidManifest(string gameId, string description = "A game for tests.") =>
        $$"""
        {
          "protocolVersion": 2,
          "gameId": "{{gameId}}",
          "displayName": "Test Game",
          "description": "{{description}}",
          "stateOwner": "client",
          "settings": []
        }
        """;

    private static byte[] BuildZip(params (string Path, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in entries)
            {
                var entry = archive.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }

    private ClientGameInstaller CreateInstaller(GameCatalog catalog) =>
        new(catalog, _pluginsDirectory, NullLoggerFactory.Instance);

    [Fact]
    public async Task Install_with_manifest_at_the_zip_root_registers_a_playable_client_game()
    {
        var catalog = new GameCatalog([]);
        var installer = CreateInstaller(catalog);
        var zip = BuildZip(("plugin.json", ValidManifest("rootgame")), ("ui/index.html", "<html></html>"));

        var result = await installer.Install(new MemoryStream(zip), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.GameId.Should().Be("rootgame");
        catalog.IsClientOwned("rootgame").Should().BeTrue();
        File.Exists(Path.Combine(_pluginsDirectory, "rootgame", "ui", "index.html")).Should().BeTrue();
    }

    [Fact]
    public async Task Install_with_manifest_inside_a_single_wrapping_folder_also_works()
    {
        var catalog = new GameCatalog([]);
        var installer = CreateInstaller(catalog);
        var zip = BuildZip(
            ("yourgame/plugin.json", ValidManifest("wrappedgame")),
            ("yourgame/ui/index.html", "<html></html>"));

        var result = await installer.Install(new MemoryStream(zip), CancellationToken.None);

        result.IsError.Should().BeFalse();
        catalog.IsClientOwned("wrappedgame").Should().BeTrue();
        File.Exists(Path.Combine(_pluginsDirectory, "wrappedgame", "ui", "index.html")).Should().BeTrue();
    }

    [Fact]
    public async Task Install_rejects_a_file_that_is_not_a_zip_archive()
    {
        var installer = CreateInstaller(new GameCatalog([]));

        var result = await installer.Install(new MemoryStream("not a zip"u8.ToArray()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.InvalidPackage");
    }

    [Fact]
    public async Task Install_rejects_a_package_with_no_plugin_json()
    {
        var installer = CreateInstaller(new GameCatalog([]));
        var zip = BuildZip(("ui/index.html", "<html></html>"));

        var result = await installer.Install(new MemoryStream(zip), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.InvalidPackage");
    }

    [Fact]
    public async Task Install_rejects_a_gameId_that_is_not_kebab_case()
    {
        var installer = CreateInstaller(new GameCatalog([]));
        var zip = BuildZip(("plugin.json", ValidManifest("Not Valid!")));

        var result = await installer.Install(new MemoryStream(zip), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.InvalidPackage");
    }

    [Fact]
    public async Task Install_rejects_a_gameId_already_used_by_a_built_in_game()
    {
        var catalog = new GameCatalog([new FakeGameFactory("x01")]);
        var installer = CreateInstaller(catalog);
        var zip = BuildZip(("plugin.json", ValidManifest("x01")));

        var result = await installer.Install(new MemoryStream(zip), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.GameIdConflict");
    }

    [Fact]
    public async Task Install_over_an_existing_client_owned_gameId_replaces_it()
    {
        var catalog = new GameCatalog([]);
        var installer = CreateInstaller(catalog);
        await installer.Install(
            new MemoryStream(BuildZip(("plugin.json", ValidManifest("upgrademe", "first version")))),
            CancellationToken.None);

        var result = await installer.Install(
            new MemoryStream(BuildZip(("plugin.json", ValidManifest("upgrademe", "second version")))),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Description.Should().Be("second version");
        catalog.ListAvailable().Should().ContainSingle(g => g.GameId == "upgrademe");
    }

    [Fact]
    public async Task Remove_deletes_a_client_owned_game_and_drops_it_from_the_catalog()
    {
        var catalog = new GameCatalog([]);
        var installer = CreateInstaller(catalog);
        await installer.Install(new MemoryStream(BuildZip(("plugin.json", ValidManifest("removeme")))), CancellationToken.None);

        var result = await installer.Remove("removeme", CancellationToken.None);

        result.IsError.Should().BeFalse();
        catalog.Resolve("removeme").IsError.Should().BeTrue();
        Directory.Exists(Path.Combine(_pluginsDirectory, "removeme")).Should().BeFalse();
    }

    [Fact]
    public async Task Remove_rejects_a_built_in_game()
    {
        var catalog = new GameCatalog([new FakeGameFactory("x01")]);
        var installer = CreateInstaller(catalog);

        var result = await installer.Remove("x01", CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.CannotRemoveBuiltIn");
    }

    [Fact]
    public async Task Remove_of_an_unknown_gameId_returns_not_found()
    {
        var installer = CreateInstaller(new GameCatalog([]));

        var result = await installer.Remove("unknown", CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("Game.NotFound");
    }
}
