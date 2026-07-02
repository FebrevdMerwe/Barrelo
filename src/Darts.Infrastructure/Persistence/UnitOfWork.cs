using Darts.Application.Common.Interfaces.Persistence;

namespace Darts.Infrastructure.Persistence;

public sealed class UnitOfWork(DartsDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
