using Darts.Application.Common.Dispatch;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Commands.Detection.RecordEndOfTurn;

public sealed record RecordEndOfTurnCommand : IRequest<ErrorOr<GameStateSnapshot>>;
