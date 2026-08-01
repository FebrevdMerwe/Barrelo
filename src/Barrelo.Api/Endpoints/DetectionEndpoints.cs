using Barrelo.Api.Common;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Detection.RecordDetectedThrow;
using Barrelo.Application.Commands.Detection.RecordEndOfTurn;
using Barrelo.Application.Commands.Detection.UndoLastThrow;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;

namespace Barrelo.Api.Endpoints;

public static class DetectionEndpoints
{
    public static void MapDetectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/detection").WithTags("Detection");

        // What the board pill reads on page load. Every change after that arrives as a
        // DetectionStatusChanged push on the game hub, so a board that drops mid-match updates the screen
        // without a refresh.
        group.MapGet("/status", async (IDetectionSource detectionSource) =>
            Results.Ok(new DetectionStatus(detectionSource.SourceType, await detectionSource.IsConnectedAsync())));

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
