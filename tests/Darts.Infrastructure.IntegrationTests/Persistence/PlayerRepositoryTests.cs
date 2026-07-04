using Darts.Domain.Entities;
using Darts.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.Persistence;

public class PlayerRepositoryTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Add_then_GetById_round_trips_a_player()
    {
        var player = Player.Create("Febre").Value;
        await using (var context = _database.CreateContext())
        {
            await new PlayerRepository(context).Add(player, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var found = await new PlayerRepository(readContext).GetById(player.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Name.Should().Be("Febre");
    }

    [Fact]
    public async Task Remove_then_SaveChanges_deletes_the_player()
    {
        var player = Player.Create("Priya Anand").Value;
        await using (var context = _database.CreateContext())
        {
            await new PlayerRepository(context).Add(player, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            var repo = new PlayerRepository(context);
            var loaded = await repo.GetById(player.Id, CancellationToken.None);
            repo.Remove(loaded!);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var found = await new PlayerRepository(readContext).GetById(player.Id, CancellationToken.None);

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByIds_returns_only_the_matching_players()
    {
        var p1 = Player.Create("A").Value;
        var p2 = Player.Create("B").Value;
        var p3 = Player.Create("C").Value;
        await using (var context = _database.CreateContext())
        {
            var repo = new PlayerRepository(context);
            await repo.Add(p1, CancellationToken.None);
            await repo.Add(p2, CancellationToken.None);
            await repo.Add(p3, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var found = await new PlayerRepository(readContext).GetByIds([p1.Id, p3.Id], CancellationToken.None);

        found.Select(p => p.Id).Should().BeEquivalentTo([p1.Id, p3.Id]);
    }
}
