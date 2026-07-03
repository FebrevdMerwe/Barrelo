using Darts.Api.Common;
using Darts.Api.Contracts;
using Darts.Application.Commands.Detection.RecordDetectedThrow;
using Darts.Application.Commands.Detection.RecordEndOfTurn;
using Darts.Application.Commands.Detection.UndoLastThrow;
using Darts.Application.Common.Dispatch;

namespace Darts.Api.Endpoints;

public static class DetectionEndpoints
{
    public static void MapDetectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/detection").WithTags("Detection");

        group.MapPost("/manual-throw", async (ManualThrowRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new RecordDetectedThrowCommand(request.Segment, request.Ring), ct);
            return result.Match(Results.Ok, errors => errors.ToProblem());
        });

        group.MapPost("/manual-end-turn", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new RecordEndOfTurnCommand(), ct);
            return result.Match(Results.Ok, errors => errors.ToProblem());
        });

        group.MapPost("/undo", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new UndoLastThrowCommand(), ct);
            return result.Match(Results.Ok, errors => errors.ToProblem());
        });
    }
}
