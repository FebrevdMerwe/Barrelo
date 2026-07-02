using Darts.Domain.Enums;

namespace Darts.Api.Contracts;

public sealed record StartMatchRequest(
    string GameId,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string>? Options,
    InputSource InputSource = InputSource.Manual);
