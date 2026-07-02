using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

// Darts.BoardSimulator deliberately has zero references to any Darts.* project — it stands in for an
// external, decoupled physical board the same way a real detector (e.g. AutoDarts) would be: the platform
// only ever talks to it over the network, via the /stream WebSocket below.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var sockets = new ConcurrentDictionary<Guid, WebSocket>();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();

app.Map("/stream", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var id = Guid.NewGuid();
    sockets[id] = socket;

    var buffer = new byte[1024];
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }
    catch (OperationCanceledException)
    {
        // Request aborted (app shutting down) — nothing to do.
    }
    catch (WebSocketException)
    {
        // Peer dropped without a clean close handshake — treat the same as a normal disconnect.
    }
    finally
    {
        sockets.TryRemove(id, out _);
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None);
    }
});

app.MapPost("/simulate/throw", async (ThrowRequest request) =>
{
    await BroadcastAsync(sockets, new { type = "throw", segment = request.Segment, ring = request.Ring });
    return Results.Ok();
});

app.MapPost("/simulate/end-turn", async () =>
{
    await BroadcastAsync(sockets, new { type = "endOfTurn" });
    return Results.Ok();
});

app.MapPost("/simulate/disconnect", async () =>
{
    foreach (var (id, socket) in sockets)
    {
        sockets.TryRemove(id, out _);
        if (socket.State == WebSocketState.Open)
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "simulated disconnect", CancellationToken.None);
    }

    return Results.Ok();
});

app.MapGet("/simulate/status", () =>
    Results.Ok(new { connected = sockets.Any(kv => kv.Value.State == WebSocketState.Open) }));

app.Run();

static async Task BroadcastAsync(ConcurrentDictionary<Guid, WebSocket> sockets, object message)
{
    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
    foreach (var (id, socket) in sockets)
    {
        if (socket.State != WebSocketState.Open)
        {
            sockets.TryRemove(id, out _);
            continue;
        }

        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            sockets.TryRemove(id, out _);
        }
    }
}

record ThrowRequest(int Segment, string Ring);
