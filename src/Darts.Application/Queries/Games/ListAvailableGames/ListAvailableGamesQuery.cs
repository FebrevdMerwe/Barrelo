using Darts.Application.Common.Dispatch;
using Darts.GameSdk;

namespace Darts.Application.Queries.Games.ListAvailableGames;

public sealed record ListAvailableGamesQuery : IRequest<IReadOnlyList<GameDescriptor>>;
