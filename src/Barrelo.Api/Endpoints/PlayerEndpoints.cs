using Barrelo.Api.Common;
using Barrelo.Api.Contracts;
using Barrelo.Application.Commands.Players.CreatePlayer;
using Barrelo.Application.Commands.Players.CreateSessionPlayer;
using Barrelo.Application.Commands.Players.DeletePlayer;
using Barrelo.Application.Commands.Players.ErasePlayer;
using Barrelo.Application.Commands.Players.RestoreSessionPlayer;
using Barrelo.Application.Commands.Players.UnbenchPlayer;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Queries.Players.ListPlayers;

namespace Barrelo.Api.Endpoints;

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

        group.MapPost("/session", async (CreatePlayerRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new CreateSessionPlayerCommand(request.Name), ct);
            return result.Match(
                player => Results.Created($"/api/players/{player.Id}", player),
                errors => errors.ToProblem());
        });

        group.MapPost("/{playerId:guid}/restore", async (Guid playerId, RestorePlayerRequest request, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new RestoreSessionPlayerCommand(playerId, request.Name, request.CreatedAtUtc), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblem());
        });

        group.MapPost("/{playerId:guid}/unbench", async (Guid playerId, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new UnbenchPlayerCommand(playerId), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblem());
        });

        group.MapDelete("/{playerId:guid}", async (Guid playerId, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new ErasePlayerCommand(playerId), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblem());
        });

        group.MapDelete("/{playerId:guid}/permanent", async (Guid playerId, IDispatcher dispatcher, CancellationToken ct) =>
        {
            var result = await dispatcher.Send(new DeletePlayerCommand(playerId), ct);
            return result.Match(
                _ => Results.NoContent(),
                errors => errors.ToProblem());
        });
    }
}
