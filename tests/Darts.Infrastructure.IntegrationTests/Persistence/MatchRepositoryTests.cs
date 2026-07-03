using Darts.Domain.Entities;
using Darts.Domain.Enums;
using Darts.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace Darts.Infrastructure.IntegrationTests.Persistence;

public class MatchRepositoryTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Add_persists_the_match_and_its_ordered_participants()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var match = Match.Start("x01", "{}", [p1, p2]).Value;

        await using (var context = _database.CreateContext())
        {
            await new MatchRepository(context).Add(match, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var found = await new MatchRepository(readContext).GetById(match.Id, CancellationToken.None);

        found.Should().NotBeNull();
        found!.Participants.Should().HaveCount(2);
        found.Participants.OrderBy(p => p.Order).Select(p => p.PlayerId).Should().Equal(p1, p2);
    }

    [Fact]
    public async Task AddThrowRecord_assigns_increasing_sequence_numbers_per_match()
    {
        var matchId = await SeedMatch();
        var playerId = Guid.NewGuid();

        await using var context = _database.CreateContext();
        var repo = new MatchRepository(context);

        var first = await repo.AddThrowRecord(matchId, playerId, 1, 1, 20, Ring.Triple, 60, "T20", 0, 1, DetectionSource.Mock, DateTimeOffset.UtcNow, CancellationToken.None);
        await context.SaveChangesAsync();
        var second = await repo.AddThrowRecord(matchId, playerId, 1, 1, 20, Ring.Double, 40, "D20", 0, 1, DetectionSource.Mock, DateTimeOffset.UtcNow, CancellationToken.None);
        await context.SaveChangesAsync();

        first.Sequence.Should().Be(1);
        second.Sequence.Should().Be(2);
    }

    [Fact]
    public async Task RemoveLastThrowRecord_removes_only_the_highest_sequence_record()
    {
        var matchId = await SeedMatch();
        var playerId = Guid.NewGuid();

        await using (var context = _database.CreateContext())
        {
            var repo = new MatchRepository(context);
            await repo.AddThrowRecord(matchId, playerId, 1, 1, 20, Ring.Triple, 60, "T20", 0, 1, DetectionSource.Mock, DateTimeOffset.UtcNow, CancellationToken.None);
            await context.SaveChangesAsync();
            await repo.AddThrowRecord(matchId, playerId, 1, 1, 20, Ring.Double, 40, "D20", 0, 1, DetectionSource.Mock, DateTimeOffset.UtcNow, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using (var context = _database.CreateContext())
        {
            await new MatchRepository(context).RemoveLastThrowRecord(matchId, CancellationToken.None);
            await context.SaveChangesAsync();
        }

        await using var readContext = _database.CreateContext();
        var remaining = await new MatchRepository(readContext).GetThrowRecords(matchId, CancellationToken.None);

        remaining.Should().ContainSingle();
        remaining[0].RawNotation.Should().Be("T20");
    }

    private async Task<Guid> SeedMatch()
    {
        var match = Match.Start("x01", "{}", [Guid.NewGuid()]).Value;
        await using var context = _database.CreateContext();
        await new MatchRepository(context).Add(match, CancellationToken.None);
        await context.SaveChangesAsync();
        return match.Id;
    }
}
