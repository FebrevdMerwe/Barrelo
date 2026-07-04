using Barrelo.Application.Common.GameExecution;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<GameCommandExecutor>();
        return services;
    }
}
