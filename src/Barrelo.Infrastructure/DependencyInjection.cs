using Barrelo.Application.Common.Constants;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Infrastructure.External.Detection;
using Barrelo.Infrastructure.External.GamePlugins;
using Barrelo.Infrastructure.External.Notifications;
using Barrelo.Infrastructure.External.Sessions;
using Barrelo.Infrastructure.Persistence;
using Barrelo.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BarreloDbContext>(options =>
            options.UseSqlite(configuration.GetConnectionString("BarreloDb") ?? "Data Source=barrelo.db"));

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<ISessionPlayerStore, SessionPlayerStore>();
        services.AddSingleton<ISessionLeaderboardStore, SessionLeaderboardStore>();
        services.AddSingleton<IReplayDivergenceMonitor, ReplayDivergenceMonitor>();
        services.AddSingleton(sp =>
        {
            var pluginsDirectory = PluginsDirectoryResolver.Resolve(configuration);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var pluginFactories = new PluginGameLoader(loggerFactory.CreateLogger<PluginGameLoader>())
                .LoadFactories(pluginsDirectory);
            var catalog = new GameCatalog(pluginFactories);

            var clientFactories = new ClientGameLoader(loggerFactory).LoadFactories(pluginsDirectory);
            catalog.ReloadClientGames(clientFactories);

            return catalog;
        });
        services.AddSingleton<IGameCatalog>(sp => sp.GetRequiredService<GameCatalog>());
        services.AddSingleton<IGameInstaller>(sp => new ClientGameInstaller(
            sp.GetRequiredService<GameCatalog>(),
            PluginsDirectoryResolver.Resolve(configuration),
            sp.GetRequiredService<ILoggerFactory>()));

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
        else if (string.Equals(detectionMode, "AutoDarts", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(sp =>
            {
                var url = configuration["Detection:AutoDarts:Url"] ?? "ws://localhost:3180/api/events";
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<AutoDartsDetectionSource>();
                return new AutoDartsDetectionSource(new Uri(url), WellKnownBoardIds.AutoDarts, logger);
            });
            services.AddSingleton<IDetectionSource>(sp => sp.GetRequiredService<AutoDartsDetectionSource>());
        }
        else
        {
            services.AddSingleton<MockDetectionSource>();
            services.AddSingleton<IDetectionSource>(sp => sp.GetRequiredService<MockDetectionSource>());
        }

        services.AddHostedService<DetectionListenerService>();

        services.AddScoped<IGameNotifier, NullGameNotifier>();
        services.AddSingleton<IDetectionStatusNotifier, NullDetectionStatusNotifier>();

        return services;
    }
}
