using System.Net;
using System.Net.Sockets;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>A minimal in-process HTTP server standing in for an out-of-process game's RPC endpoint in
/// tests — lets RemoteGame's HTTP-client behavior be exercised without spawning any real external
/// process/runtime (Node, Python, etc.), keeping `dotnet test` pure-.NET.</summary>
internal sealed class FakeGameServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public int Port { get; }

    public string BaseUrl => $"http://127.0.0.1:{Port}/";

    /// <summary>Keyed by "METHOD /path". Missing routes 404.</summary>
    public Dictionary<string, Func<HttpListenerContext, Task>> Routes { get; } = new();

    public FakeGameServer()
    {
        Port = GetFreePort();
        _listener.Prefixes.Add(BaseUrl);
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(RunLoop);
    }

    private async Task RunLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch
            {
                return; // listener stopped/disposed
            }

            _ = Handle(context);
        }
    }

    private async Task Handle(HttpListenerContext context)
    {
        try
        {
            var key = context.Request.HttpMethod + " " + context.Request.Url!.AbsolutePath;
            if (Routes.TryGetValue(key, out var route))
                await route(context);
            else
                context.Response.StatusCode = 404;
        }
        catch
        {
            context.Response.StatusCode = 500;
        }
        finally
        {
            context.Response.Close();
        }
    }

    /// <summary>Stops accepting/serving requests immediately, simulating a crashed remote game process
    /// without needing to kill a real one — RemoteGame only ever reacts to HTTP-call failures.</summary>
    public void StopAbruptly()
    {
        _cts.Cancel();
        if (_listener.IsListening)
            _listener.Stop();
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        StopAbruptly();
        _listener.Close();
        if (_loop is not null)
        {
            try { await _loop; } catch { /* listener stopped */ }
        }
    }
}
