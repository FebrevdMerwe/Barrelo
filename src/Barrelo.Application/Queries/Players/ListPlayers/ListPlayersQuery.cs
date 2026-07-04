using Barrelo.Application.Common.Dispatch;

namespace Barrelo.Application.Queries.Players.ListPlayers;

public sealed record ListPlayersQuery : IRequest<IReadOnlyList<PlayerListItem>>;
