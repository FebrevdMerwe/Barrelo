using Darts.GameSdk;

namespace Darts.Application.Commands.Matches.StartMatch;

public sealed record StartMatchResult(Guid MatchId, GameStateSnapshot InitialState);
