using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Infrastructure.External.Detection;
using Darts.Infrastructure.External.GamePlugins;
using Darts.Infrastructure.Persistence;
using Darts.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Darts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DartsDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("DartsDb") ?? "Data Source=darts.db"));

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<IGameCatalog>(sp =>
        {
            var pluginsDirectory = ResolvePluginsDirectory(configuration["Plugins:Directory"] ?? "plugins");
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PluginGameLoader>();
            var factories = new PluginGameLoader(logger).LoadFactories(pluginsDirectory);
            return new GameCatalog(factories);
        });

        services.AddSingleton<MockDetectionSource>();
        services.AddSingleton<IDetectionSource>(sp => sp.GetRequiredService<MockDetectionSource>());

        return services;
    }

    private static string ResolvePluginsDirectory(string configured) =>
        Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
}
