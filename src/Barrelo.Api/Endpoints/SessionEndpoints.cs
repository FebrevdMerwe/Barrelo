using Barrelo.Api.Common;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Queries.Matches.GetMatchState;

namespace Barrelo.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/session/current", async (
            IGameSessionManager sessionManager, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var matchId = await sessionManager.TryGetActiveMatchIdAsync();
            if (matchId is null)
                return Results.Ok(new CurrentSessionResponse(false, null));

            var result = await dispatcher.Send(new GetMatchStateQuery(matchId.Value), ct);
            return result.Match(
                snapshot => Results.Ok(new CurrentSessionResponse(true, snapshot)),
                errors => errors.ToProblem());
        }).WithTags("Session");
    }

    private sealed record CurrentSessionResponse(bool HasActiveMatch, MatchStateSnapshotDto? Snapshot);
}
