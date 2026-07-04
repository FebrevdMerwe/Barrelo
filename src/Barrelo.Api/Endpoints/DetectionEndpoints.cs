using Barrelo.Api.Common;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Detection.RecordDetectedThrow;
using Barrelo.Application.Commands.Detection.RecordEndOfTurn;
using Barrelo.Application.Commands.Detection.UndoLastThrow;
using Barrelo.Application.Common.Dispatch;

namespace Barrelo.Api.Endpoints;

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
