using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;

namespace Barrelo.Application.Queries.Games.ListAvailableGames;

public sealed class ListAvailableGamesQueryHandler(IGameCatalog catalog)
    : IRequestHandler<ListAvailableGamesQuery, IReadOnlyList<GameDescriptor>>
{
    public Task<IReadOnlyList<GameDescriptor>> Handle(ListAvailableGamesQuery request, CancellationToken ct) =>
        Task.FromResult(catalog.ListAvailable());
}
