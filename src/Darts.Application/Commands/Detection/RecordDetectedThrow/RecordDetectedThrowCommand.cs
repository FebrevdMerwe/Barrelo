using Darts.Application.Common.Dispatch;
using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Commands.Detection.RecordDetectedThrow;

public sealed record RecordDetectedThrowCommand(int Segment, Ring Ring) : IRequest<ErrorOr<GameStateSnapshot>>;
