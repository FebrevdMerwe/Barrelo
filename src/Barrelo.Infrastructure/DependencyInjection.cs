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
        services.AddSingleton<IGameCatalog>(sp =>
        {
            var pluginsDirectory = PluginsDirectoryResolver.Resolve(configuration);
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            var pluginFactories = new PluginGameLoader(loggerFactory.CreateLogger<PluginGameLoader>())
                .LoadFactories(pluginsDirectory);
            var remoteFactories = new RemoteGameLoader(loggerFactory).LoadFactories(pluginsDirectory);

            return new GameCatalog(pluginFactories.Concat(remoteFactories));
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
