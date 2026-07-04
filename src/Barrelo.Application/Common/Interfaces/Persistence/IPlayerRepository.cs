using Barrelo.Domain.Entities;

namespace Barrelo.Application.Common.Interfaces.Persistence;

public interface IPlayerRepository
{
    Task Add(Player player, CancellationToken ct);

    void Remove(Player player);

    Task<Player?> GetById(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Player>> GetByIds(IReadOnlyList<Guid> ids, CancellationToken ct);

    Task<IReadOnlyList<Player>> GetAll(CancellationToken ct);
}
