using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Darts.Infrastructure.IntegrationTests.Persistence;

public class DartsDbContextMigrationTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Migration_creates_the_expected_tables()
    {
        await using var context = _database.CreateContext();
        var connection = context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '\\_\\_EF%' ESCAPE '\\'";

        var tableNames = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tableNames.Add(reader.GetString(0));

        tableNames.Should().BeEquivalentTo(["Players"]);
    }
}
