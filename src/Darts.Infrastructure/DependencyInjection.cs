using Darts.Application.Common.Constants;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Infrastructure.External.Detection;
using Darts.Infrastructure.External.GamePlugins;
using Darts.Infrastructure.External.Notifications;
using Darts.Infrastructure.External.Sessions;
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
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<ISessionPlayerStore, SessionPlayerStore>();
        services.AddSingleton<ISessionLeaderboardStore, SessionLeaderboardStore>();
        services.AddSingleton<IGameCatalog>(sp =>
        {
            var pluginsDirectory = PluginsDirectoryResolver.Resolve(configuration);
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<PluginGameLoader>();
            var factories = new PluginGameLoader(logger).LoadFactories(pluginsDirectory);
            return new GameCatalog(factories);
        });

        var detectionMode = configuration["Detection:Mode"] ?? "Mock";
        if (string.Equals(detectionMode, "Simulator", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(sp =>
            {
                var url = configuration["Detection:Simulator:Url"] ?? "ws://localhost:5250/stream";
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<BoardSimulatorDetectionSource>();
                return new BoardSimulatorDetectionSource(new Uri(url), WellKnownBoardIds.Simulator, logger);
            });
            services.AddSingleton<IDetectionSource>(sp => sp.GetRequiredService<BoardSimulatorDetectionSource>());
        }
        else
        {
            services.AddSingleton<MockDetectionSource>();
            services.AddSingleton<IDetectionSource>(sp => sp.GetRequiredService<MockDetectionSource>());
        }

        services.AddHostedService<DetectionListenerService>();

        services.AddScoped<IGameNotifier, NullGameNotifier>();

        return services;
    }
}
