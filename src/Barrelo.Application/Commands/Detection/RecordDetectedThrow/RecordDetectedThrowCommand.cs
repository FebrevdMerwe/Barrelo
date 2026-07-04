using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Commands.Detection.RecordDetectedThrow;

public sealed record RecordDetectedThrowCommand(int Segment, Ring Ring) : IRequest<ErrorOr<MatchStateSnapshotDto>>;
