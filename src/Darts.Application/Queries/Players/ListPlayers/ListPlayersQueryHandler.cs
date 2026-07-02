using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Domain.Entities;

namespace Darts.Application.Queries.Players.ListPlayers;

public sealed class ListPlayersQueryHandler(IPlayerRepository playerRepository)
    : IRequestHandler<ListPlayersQuery, IReadOnlyList<Player>>
{
    public Task<IReadOnlyList<Player>> Handle(ListPlayersQuery request, CancellationToken ct) =>
        playerRepository.GetAll(ct);
}
