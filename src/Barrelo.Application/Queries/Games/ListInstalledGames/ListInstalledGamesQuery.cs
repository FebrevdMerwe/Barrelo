using Barrelo.Application.Common.Dispatch;

namespace Barrelo.Application.Queries.Games.ListInstalledGames;

public sealed record ListInstalledGamesQuery : IRequest<IReadOnlyList<InstalledGameItem>>;
