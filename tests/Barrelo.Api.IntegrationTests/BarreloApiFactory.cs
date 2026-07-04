using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Barrelo.Api.IntegrationTests;

/// <summary>
/// Real SQLite over a per-instance temp file (not WebApplicationFactory's inferred content root or the
/// production barrelo.db) and an explicit Plugins:Directory pointing at this test project's own copied
/// plugins/ folder — sidesteps WebApplicationFactory's content-root guessing for plugin discovery.
/// Program.cs's own startup migration step runs unmodified, so this also exercises the real startup path.
/// </summary>
public sealed class BarreloApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"darts-apitests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:BarreloDb", $"Data Source={_dbPath}");
        builder.UseSetting("Plugins:Directory", Path.Combine(AppContext.BaseDirectory, "plugins"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        // The host disposing its DbContext instances doesn't release Microsoft.Data.Sqlite's own
        // pooled connection to the file — clear the pool first or file deletion below races a lock.
        SqliteConnection.ClearAllPools();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
