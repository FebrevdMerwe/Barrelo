using Darts.Application.Common.Interfaces.Persistence;
using Darts.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Darts.Api.IntegrationTests;

internal static class PlayerSeeding
{
    /// <summary>No player-creation endpoint exists yet (Phase 2 scope) — tests seed directly via the repository.</summary>
    public static async Task<List<Guid>> SeedPlayers(this DartsApiFactory factory, params string[] names)
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
