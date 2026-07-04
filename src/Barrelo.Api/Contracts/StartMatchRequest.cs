namespace Barrelo.Api.Contracts;

public sealed record StartMatchRequest(
    string GameId,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string>? Options,
    IReadOnlyDictionary<Guid, int>? PlayerGroups = null);
