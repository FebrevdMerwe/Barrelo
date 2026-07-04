using Barrelo.GameSdk;

namespace Barrelo.Application.Commands.Matches.StartMatch;

public sealed record StartMatchResult(Guid MatchId, GameStateSnapshot InitialState);
