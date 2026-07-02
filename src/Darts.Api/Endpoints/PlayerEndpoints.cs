using Darts.Api.Common;
using Darts.Api.Contracts;
using Darts.Application.Commands.Players.CreatePlayer;
using Darts.Application.Common.Dispatch;
using Darts.Application.Queries.Players.ListPlayers;

namespace Darts.Api.Endpoints;

public static class PlayerEndpoints
{
    public static void MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/players").WithTags("Players");

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken ct) =>
        {
            var players = await dispatcher.Send(new ListPlayersQuery(), ct);
            return Results.Ok(players);
        });

        group.MapPost("/", async (CreatePlayerRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new CreatePlayerCommand(request.Name), ct);
            return result.Match(
                player => Results.Created($"/api/players/{player.Id}", player),
                errors => errors.ToProblem());
        });
    }
}
