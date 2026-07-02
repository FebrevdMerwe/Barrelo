using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Services;
using Darts.GameSdk;

namespace Darts.Application.Queries.Games.ListAvailableGames;

public sealed class ListAvailableGamesQueryHandler(IGameCatalog catalog)
    : IRequestHandler<ListAvailableGamesQuery, IReadOnlyList<GameDescriptor>>
{
    public Task<IReadOnlyList<GameDescriptor>> Handle(ListAvailableGamesQuery request, CancellationToken ct) =>
        Task.FromResult(catalog.ListAvailable());
}
