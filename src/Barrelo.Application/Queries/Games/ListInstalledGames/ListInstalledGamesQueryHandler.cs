using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;

namespace Barrelo.Application.Queries.Games.ListInstalledGames;

public sealed class ListInstalledGamesQueryHandler(IGameCatalog catalog)
    : IRequestHandler<ListInstalledGamesQuery, IReadOnlyList<InstalledGameItem>>
{
    public Task<IReadOnlyList<InstalledGameItem>> Handle(ListInstalledGamesQuery request, CancellationToken ct)
    {
        IReadOnlyList<InstalledGameItem> items = catalog.ListAvailable()
            .Select(g => new InstalledGameItem(g.GameId, g.DisplayName, g.Description, catalog.IsClientOwned(g.GameId)))
            .OrderBy(g => g.DisplayName)
            .ToList();

        return Task.FromResult(items);
    }
}
