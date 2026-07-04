using Barrelo.Api.Common;
using Barrelo.Application.Commands.Leaderboard.ResetLeaderboard;
using Barrelo.Application.Common.Dispatch;

namespace Barrelo.Api.Endpoints;

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
