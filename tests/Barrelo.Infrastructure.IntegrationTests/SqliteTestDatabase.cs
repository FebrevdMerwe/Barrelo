using Barrelo.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Barrelo.Infrastructure.IntegrationTests;

/// <summary>Real SQLite over an open in-memory connection, migrated once per test — not the EF InMemory provider, since this layer exists to validate the actual relational schema/migration.</summary>
public sealed class SqliteTestDatabase : IAsyncLifetime
{
    private SqliteConnection _connection = null!;

    public BarreloDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BarreloDbContext>().UseSqlite(_connection).Options;
        return new BarreloDbContext(options);
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();
}
