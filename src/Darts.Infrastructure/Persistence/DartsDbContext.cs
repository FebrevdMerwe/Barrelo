using Darts.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Darts.Infrastructure.Persistence;

public sealed class DartsDbContext(DbContextOptions<DartsDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DartsDbContext).Assembly);
    }
}
