using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Application.Common.Dispatch;

public static class DispatcherServiceCollectionExtensions
{
    public static IServiceCollection AddDartsDispatcher(this IServiceCollection services)
    {
        // Scoped, not singleton: handlers are registered scoped (they depend on the scoped DbContext),
        // and a singleton holding a captured root IServiceProvider would resolve them as captive
        // dependencies — sharing one DbContext across every request for the app's lifetime.
        services.AddScoped<IDispatcher, Dispatcher>();

        var assembly = typeof(DispatcherServiceCollectionExtensions).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            foreach (var implementedInterface in type.GetInterfaces())
            {
                if (!implementedInterface.IsGenericType)
                    continue;

                var definition = implementedInterface.GetGenericTypeDefinition();
                if (definition == typeof(IRequestHandler<,>) || definition == typeof(INotificationHandler<>))
                    services.AddScoped(implementedInterface, type);
            }
        }

        return services;
    }
}
