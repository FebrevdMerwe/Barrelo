using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Queries.Games.ListAvailableGames;

namespace Barrelo.Api.Endpoints;

public static class GameEndpoints
{
    public static void MapGameEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/games").WithTags("Games");

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var games = await dispatcher.Send(new ListAvailableGamesQuery(), ct);
            return Results.Ok(games);
        });
    }
}
