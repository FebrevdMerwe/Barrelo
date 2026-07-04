using Darts.Application.Common.Constants;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.GameExecution;
using Darts.GameSdk;
using ErrorOr;
using FluentValidation;

namespace Darts.Application.Commands.Detection.RecordDetectedThrow;

public sealed class RecordDetectedThrowCommandHandler(
    GameCommandExecutor executor,
    IValidator<RecordDetectedThrowCommand> validator)
    : IRequestHandler<RecordDetectedThrowCommand, ErrorOr<MatchStateSnapshotDto>>
{
    public async Task<ErrorOr<MatchStateSnapshotDto>> Handle(RecordDetectedThrowCommand request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
            return validation.Errors.Select(e => Error.Validation(e.PropertyName, e.ErrorMessage)).ToList();

        var detectedThrow = new DetectedThrow(
            ThrowId: Guid.NewGuid(),
            Segment: request.Segment,
            Ring: request.Ring,
            Score: DartScoring.Score(request.Ring, request.Segment),
            RawNotation: DartScoring.Notation(request.Ring, request.Segment),
            Position: BoardGeometry.CenterOf(request.Segment, request.Ring),
            Confidence: null,
            BoardId: WellKnownBoardIds.Manual,
            CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow,
            Source: DetectionSourceType.Manual);

        return await executor.RecordThrow(detectedThrow, ct);
    }
}
