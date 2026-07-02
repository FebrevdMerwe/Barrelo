using Darts.Application.Common.Dispatch;
using Darts.Domain.Enums;
using ErrorOr;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed record StartMatchCommand(
    string GameId,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string> Options,
    InputSource InputSource) : IRequest<ErrorOr<StartMatchResult>>;
