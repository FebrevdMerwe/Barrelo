using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;

namespace Darts.Application.Queries.Players.ListPlayers;

public sealed class ListPlayersQueryHandler(
    IPlayerRepository playerRepository,
    ISessionPlayerStore sessionPlayerStore)
    : IRequestHandler<ListPlayersQuery, IReadOnlyList<PlayerListItem>>
{
    public async Task<IReadOnlyList<PlayerListItem>> Handle(ListPlayersQuery request, CancellationToken ct)
    {
        var permanentPlayers = await playerRepository.GetAll(ct);
        var benchedIds = sessionPlayerStore.GetBenchedPermanentPlayerIds();

        var items = permanentPlayers
            .Select(p => new PlayerListItem(p.Id, p.Name, p.CreatedAtUtc, IsPermanent: true, IsBenched: benchedIds.Contains(p.Id)))
            .Concat(sessionPlayerStore.GetAllSessionPlayers()
                .Select(p => new PlayerListItem(p.Id, p.Name, p.CreatedAtUtc, IsPermanent: false, IsBenched: false)));

        return items.OrderBy(p => p.Name).ToList();
    }
}
