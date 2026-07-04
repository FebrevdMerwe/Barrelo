using Darts.Application.Common.Dispatch;

namespace Darts.Application.Queries.Players.ListPlayers;

public sealed record ListPlayersQuery : IRequest<IReadOnlyList<PlayerListItem>>;
