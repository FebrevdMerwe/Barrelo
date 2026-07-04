using Barrelo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barrelo.Infrastructure.Persistence;

public sealed class BarreloDbContext(DbContextOptions<BarreloDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BarreloDbContext).Assembly);
    }
}
