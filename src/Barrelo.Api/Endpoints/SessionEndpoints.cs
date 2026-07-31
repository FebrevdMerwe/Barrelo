using Barrelo.Api.Common;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Matches.ReportMatchResult;
using Barrelo.Application.Commands.Matches.ReportReplayHash;
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

        // A client-owned game's rules run in the browser, so the host is told the outcome here rather
        // than working it out. The handler checks the reported ids against the match's roster before any
        // leaderboard points are awarded.
        app.MapPost("/api/session/result", async (
            ReportMatchResultRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(
                new ReportMatchResultCommand(request.WinnerPlayerIds, request.FinalStandings), ct);
            return result.Match(Results.Ok, errors => errors.ToProblem());
        }).WithTags("Session");

        // Advisory only — see IReplayDivergenceMonitor. The signal goes to the log, not back to the
        // client that reported it, so there's nothing useful to return.
        app.MapPost("/api/session/state-hash", async (
            ReportReplayHashRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(
                new ReportReplayHashCommand(request.LogHash, request.StateHash), ct);
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblem());
        }).WithTags("Session");
    }

    private sealed record CurrentSessionResponse(bool HasActiveMatch, MatchStateSnapshotDto? Snapshot);
}
