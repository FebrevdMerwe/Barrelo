using Darts.Api.Common;
using Darts.Api.Contracts;
using Darts.Application.Commands.Players.CreatePlayer;
using Darts.Application.Commands.Players.CreateSessionPlayer;
using Darts.Application.Commands.Players.DeletePlayer;
using Darts.Application.Commands.Players.ErasePlayer;
using Darts.Application.Commands.Players.RestoreSessionPlayer;
using Darts.Application.Commands.Players.UnbenchPlayer;
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
