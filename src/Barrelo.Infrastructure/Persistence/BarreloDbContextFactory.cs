using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Barrelo.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef migrations` run against this project alone, without booting the Api's full DI graph.</summary>
public sealed class BarreloDbContextFactory : IDesignTimeDbContextFactory<BarreloDbContext>
{
    public BarreloDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BarreloDbContext>();
        optionsBuilder.UseSqlite("Data Source=barrelo.db");
        return new BarreloDbContext(optionsBuilder.Options);
    }
}
