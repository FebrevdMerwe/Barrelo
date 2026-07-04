using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Api.IntegrationTests;

internal static class PlayerSeeding
{
    /// <summary>Seeds permanent players directly via the repository, bypassing the HTTP API for test setup speed.</summary>
    public static async Task<List<Guid>> SeedPlayers(this BarreloApiFactory factory, params string[] names)
    {
        using var scope = factory.Services.CreateScope();
        var playerRepository = scope.ServiceProvider.GetRequiredService<IPlayerRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var ids = new List<Guid>();
        foreach (var name in names)
        {
            var player = Player.Create(name).Value;
            await playerRepository.Add(player, CancellationToken.None);
            ids.Add(player.Id);
        }

        await unitOfWork.SaveChangesAsync(CancellationToken.None);
        return ids;
    }
}
