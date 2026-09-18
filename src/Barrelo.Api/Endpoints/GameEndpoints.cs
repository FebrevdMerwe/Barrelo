using Barrelo.Api.Common;
using Barrelo.Application.Commands.Games.InstallGame;
using Barrelo.Application.Commands.Games.RemoveGame;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Queries.Games.ListAvailableGames;
using Barrelo.Application.Queries.Games.ListInstalledGames;

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

        group.MapGet("/installed", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var games = await dispatcher.Send(new ListInstalledGamesQuery(), ct);
            return Results.Ok(games);
        });

        group.MapPost("/installed", async (IFormFile file, IDispatcher dispatcher, CancellationToken ct) =>
        {
            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);

            var result = await dispatcher.Send(new InstallGameCommand(stream.ToArray()), ct);
            return result.Match(
                game => Results.Created($"/api/games/installed/{game.GameId}", game),
                errors => errors.ToProblem());
        }).DisableAntiforgery();

        group.MapDelete("/installed/{gameId}", async (string gameId, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new RemoveGameCommand(gameId), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblem());
        });
    }
}
