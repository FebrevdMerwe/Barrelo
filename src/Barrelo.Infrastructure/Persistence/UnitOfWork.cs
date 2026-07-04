using Barrelo.Application.Common.Interfaces.Persistence;

namespace Barrelo.Infrastructure.Persistence;

public sealed class UnitOfWork(BarreloDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => context.SaveChangesAsync(ct);
}
