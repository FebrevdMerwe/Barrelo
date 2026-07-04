using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Barrelo.Infrastructure.Persistence.Repositories;

public sealed class PlayerRepository(BarreloDbContext context) : IPlayerRepository
{
    public async Task Add(Player player, CancellationToken ct) => await context.Players.AddAsync(player, ct);

    public void Remove(Player player) => context.Players.Remove(player);

    public Task<Player?> GetById(Guid id, CancellationToken ct) =>
        context.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Player>> GetByIds(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await context.Players.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Player>> GetAll(CancellationToken ct) =>
        
        await context.Players.OrderBy(p => p.Name).ToListAsync(ct);
}
