using Darts.Application.Common.Dispatch;
using Darts.Domain.Entities;

namespace Darts.Application.Queries.Players.ListPlayers;

public sealed record ListPlayersQuery : IRequest<IReadOnlyList<Player>>;
