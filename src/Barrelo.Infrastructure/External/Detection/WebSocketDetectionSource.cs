using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>
/// Shared connect/reconnect machinery for every detector that speaks WebSocket. A subclass decides only
/// how one received text message becomes detection events (<see cref="ParseMessage"/>); staying alive is
/// this class's problem, in one place, so a fix here fixes every detector at once.
///
/// Each piece of that exists because of a specific way a board stops scoring for the rest of the evening:
/// <list type="bullet">
/// <item>The loop never exits. Every failure is logged and retried with capped backoff, so a board manager
/// that is restarted mid-match is picked back up on its own.</item>
/// <item>A message that can't be interpreted is dropped with a log line instead of tearing down the socket.
/// One unrecognised payload used to cost a reconnect, and with it the darts either side of it.</item>
/// <item>Keep-alive runs with a <em>timeout</em>. The drop that actually strands a board is the one with no
/// close frame — the far end is simply gone. Without a ping deadline <c>ReceiveAsync</c> waits forever, the
/// socket still reads as Open, and nothing ever reconnects.</item>
/// <item>Connect has its own deadline, so an endpoint that accepts TCP but never finishes the handshake
/// can't wedge the loop either.</item>
/// </list>
///
/// Connection transitions are published onto the same channel as darts rather than raised as callbacks, so
/// the single consumer sees them in order with the throws around them and nothing else in the platform has
/// to know which detector is plugged in.
///
/// Subclasses must be stateless parsers: the connection loop starts in this constructor and can call
/// <see cref="ParseMessage"/> before a subclass field initialiser has run.
/// </summary>
public abstract class WebSocketDetectionSource : IDetectionSource, IAsyncDisposable
{
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan KeepAlivePing = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan KeepAliveDeadline = TimeSpan.FromSeconds(10);

    private readonly Uri _uri;
    private readonly string _displayName;
    private readonly Channel<DetectionEvent> _channel = Channel.CreateUnbounded<DetectionEvent>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _connectionLoop;

    private volatile bool _connected;
    private int _disposed;

    protected WebSocketDetectionSource(Uri uri, string boardId, string displayName, ILogger logger)
    {
        _uri = uri;
        _displayName = displayName;
        BoardId = boardId;
        Logger = logger;
        _connectionLoop = Task.Run(() => RunAsync(_cts.Token));
    }

    public abstract DetectionSourceType SourceType { get; }

    protected string BoardId { get; }

    protected ILogger Logger { get; }

    public IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public Task<bool> IsConnectedAsync() => Task.FromResult(_connected);

    /// <summary>Maps one received text message onto zero or more detection events. Throwing is safe: the
    /// caller logs the payload and drops the message, leaving the connection up.</summary>
    protected abstract IEnumerable<DetectionEvent> ParseMessage(string json);

    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = InitialBackoff;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    using var socket = new ClientWebSocket();

                    // Ping on an interval *and* fail the socket when a pong doesn't come back. The interval
                    // alone (the framework default) only keeps intermediaries from timing the connection
                    // out; it is the deadline that turns a silently dead peer into a reconnect.
                    socket.Options.KeepAliveInterval = KeepAlivePing;
                    socket.Options.KeepAliveTimeout = KeepAliveDeadline;

                    await ConnectAsync(socket, ct);

                    Logger.LogInformation("Connected to {Source} at {Uri}.", _displayName, _uri);
                    backoff = InitialBackoff;
                    SetConnected(true);

                    await ReceiveLoop(socket, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    Logger.LogWarning(ex, "{Source} connection lost; reconnecting in {Delay}.", _displayName, backoff);
                }
                finally
                {
                    SetConnected(false);
                }

                if (ct.IsCancellationRequested)
                    break;

                try { await Task.Delay(backoff, ct); }
                catch (OperationCanceledException) { break; }

                backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
            }
        }
        finally
        {
            SetConnected(false);
        }
    }

    private async Task ConnectAsync(ClientWebSocket socket, CancellationToken ct)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        connectCts.CancelAfter(ConnectTimeout);

        try
        {
            await socket.ConnectAsync(_uri, connectCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Translate the deadline into something the reconnect loop treats as an ordinary failure —
            // an OperationCanceledException here would otherwise read as "we're shutting down".
            throw new TimeoutException($"Connecting to {_displayName} at {_uri} timed out after {ConnectTimeout}.");
        }
    }

    private async Task ReceiveLoop(ClientWebSocket socket, CancellationToken ct)
    {
        var buffer = new byte[4096];
        while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Logger.LogWarning(
                        "{Source} closed the connection ({Status}: {Description}).",
                        _displayName, socket.CloseStatus, socket.CloseStatusDescription);
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                Logger.LogDebug("Ignoring an unexpected binary message from {Source}.", _displayName);
                continue;
            }

            Publish(Encoding.UTF8.GetString(ms.ToArray()));
        }
    }

    private void Publish(string json)
    {
        List<DetectionEvent> events;
        try
        {
            // Materialised before anything is written so a parser that throws part-way emits nothing at
            // all — half a visit is worse than none, since the differ would then treat the rest as new.
            events = ParseMessage(json).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Dropping a {Source} message that could not be interpreted: {Json}", _displayName, json);
            return;
        }

        foreach (var evt in events)
            _channel.Writer.TryWrite(evt);
    }

    /// <summary>Announces a transition only. The connection loop is the sole writer, so no synchronisation
    /// beyond the volatile read is needed.</summary>
    private void SetConnected(bool connected)
    {
        if (_connected == connected)
            return;

        _connected = connected;
        _channel.Writer.TryWrite(DetectionEvent.ConnectionChanged(BoardId, connected));
    }

    public async ValueTask DisposeAsync()
    {
        // Registered under both its own type and IDetectionSource (DependencyInjection.cs), so the DI
        // container resolves and disposes this same singleton instance twice on shutdown.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _cts.CancelAsync();
        try
        {
            await _connectionLoop;
        }
        catch
        {
            // Connection loop observes cancellation internally; nothing actionable on shutdown.
        }

        _channel.Writer.TryComplete();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
