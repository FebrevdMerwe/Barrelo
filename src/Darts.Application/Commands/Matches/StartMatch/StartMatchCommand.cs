using Darts.Application.Common.Dispatch;
using ErrorOr;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed record StartMatchCommand(
    string GameId,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string> Options,
    IReadOnlyDictionary<Guid, int>? PlayerGroups = null) : IRequest<ErrorOr<StartMatchResult>>;
