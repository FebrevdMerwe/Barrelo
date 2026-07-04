using Barrelo.Api.Common;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Queries.Matches.GetMatchState;

namespace Barrelo.Api.Endpoints;

public static class MatchEndpoints
{
    public static void MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/matches").WithTags("Matches");

        group.MapPost("/", async (StartMatchRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var command = new StartMatchCommand(
                request.GameId,
                request.PlayerIds,
                request.Options ?? new Dictionary<string, string>(),
                request.PlayerGroups);

            var result = await dispatcher.Send(command, ct);

            return result.Match(
                success => Results.Created($"/api/matches/{success.MatchId}", success),
                errors => errors.ToProblem());
        });

        group.MapGet("/{matchId:guid}", async (Guid matchId, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new GetMatchStateQuery(matchId), ct);
            return result.Match(Results.Ok, errors => errors.ToProblem());
        });
    }
}
