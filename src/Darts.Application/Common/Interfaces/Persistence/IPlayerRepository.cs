using Darts.Domain.Entities;

namespace Darts.Application.Common.Interfaces.Persistence;

public interface IPlayerRepository
{
    Task Add(Player player, CancellationToken ct);

    Task<Player?> GetById(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Player>> GetByIds(IReadOnlyList<Guid> ids, CancellationToken ct);
}
