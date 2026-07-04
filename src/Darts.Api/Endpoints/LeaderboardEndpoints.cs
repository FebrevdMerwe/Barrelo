using Darts.Api.Common;
using Darts.Application.Commands.Leaderboard.ResetLeaderboard;
using Darts.Application.Common.Dispatch;

namespace Darts.Api.Endpoints;

public static class LeaderboardEndpoints
{
    public static void MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leaderboard").WithTags("Leaderboard");

        group.MapPost("/reset", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new ResetLeaderboardCommand(), ct);
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblem());
        });
    }
}
