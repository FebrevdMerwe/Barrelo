using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Darts.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations` run against this project alone, without booting the Api's full DI graph.</summary>
public sealed class DartsDbContextFactory : IDesignTimeDbContextFactory<DartsDbContext>
{
    public DartsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<DartsDbContext>();
        optionsBuilder.UseSqlite("Data Source=darts.db");
        return new DartsDbContext(optionsBuilder.Options);
    }
}
