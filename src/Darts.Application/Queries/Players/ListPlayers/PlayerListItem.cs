namespace Darts.Application.Queries.Players.ListPlayers;

public sealed record PlayerListItem(Guid Id, string Name, DateTimeOffset CreatedAtUtc, bool IsPermanent, bool IsBenched);
