using Darts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Darts.Infrastructure.Persistence;

public sealed class DartsDbContext(DbContextOptions<DartsDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();

    public DbSet<Match> Matches => Set<Match>();

    public DbSet<ThrowRecord> ThrowRecords => Set<ThrowRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DartsDbContext).Assembly);
    }
}
