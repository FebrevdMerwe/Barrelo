namespace Barrelo.Application.Queries.Games.ListInstalledGames;

public sealed record InstalledGameItem(string GameId, string DisplayName, string Description, bool Removable);
