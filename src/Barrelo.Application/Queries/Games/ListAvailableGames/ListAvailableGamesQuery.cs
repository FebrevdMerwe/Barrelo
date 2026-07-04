using Barrelo.Application.Common.Dispatch;
using Barrelo.GameSdk;

namespace Barrelo.Application.Queries.Games.ListAvailableGames;

public sealed record ListAvailableGamesQuery : IRequest<IReadOnlyList<GameDescriptor>>;
