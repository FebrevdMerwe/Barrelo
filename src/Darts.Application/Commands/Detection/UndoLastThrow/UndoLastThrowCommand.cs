using Darts.Application.Common.Dispatch;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Commands.Detection.UndoLastThrow;

public sealed record UndoLastThrowCommand : IRequest<ErrorOr<GameStateSnapshot>>;
