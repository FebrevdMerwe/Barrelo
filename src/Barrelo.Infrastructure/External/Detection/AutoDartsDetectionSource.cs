using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>
/// Connects to a local AutoDarts board-manager event stream (ws://host:3180/api/events) and maps its
/// per-visit "state" events onto the canonical, detector-agnostic DetectedThrow.
///
/// AutoDarts sends the *cumulative* set of darts for the current visit on every "Throw detected" event
/// (numThrows/throws grows to up to 3), not one message per dart, so this adapter diffs against the
/// last-seen throws to emit one DetectionEvent.Throw per new dart. Turn boundary is the "Takeout finished"
/// event (board cleared, numThrows back to 0) rather than "Takeout started", so EndOfTurn only fires once
/// the visit is fully wrapped up.
/// </summary>
public sealed class AutoDartsDetectionSource : IDetectionSource, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly Uri _eventsUri;
    private readonly string _boardId;
    private readonly ILogger<AutoDartsDetectionSource> _logger;
    private readonly Channel<DetectionEvent> _channel = Channel.CreateUnbounded<DetectionEvent>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _connectionLoop;

    private volatile ClientWebSocket? _socket;
    private int _disposed;
    private IReadOnlyList<AutoDartsThrow> _lastThrows = [];

    public AutoDartsDetectionSource(Uri eventsUri, string boardId, ILogger<AutoDartsDetectionSource> logger)
    {
        _eventsUri = eventsUri;
        _boardId = boardId;
        _logger = logger;
        _connectionLoop = Task.Run(() => RunAsync(_cts.Token));
    }

    public IAsyncEnumerable<DetectionEvent> EventsAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);

    public Task<bool> IsConnectedAsync() => Task.FromResult(_socket?.State == WebSocketState.Open);

    private async Task RunAsync(CancellationToken ct)
    {
        var backoff = InitialBackoff;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var socket = new ClientWebSocket();
                await socket.ConnectAsync(_eventsUri, ct);
                _socket = socket;
                _logger.LogInformation("Connected to AutoDarts board manager at {Uri}", _eventsUri);
                backoff = InitialBackoff;

                await ReceiveLoop(socket, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "AutoDarts connection lost; reconnecting in {Delay}", backoff);
            }
            finally
            {
                _socket = null;
            }

            if (ct.IsCancellationRequested)
                break;

            try { await Task.Delay(backoff, ct); }
            catch (OperationCanceledException) { break; }

            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, MaxBackoff.TotalSeconds));
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
                    _logger.LogWarning("AutoDarts board manager closed the connection.");
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            foreach (var evt in ParseMessage(json))
                _channel.Writer.TryWrite(evt);
        }
    }

    private IEnumerable<DetectionEvent> ParseMessage(string json)
    {
        AutoDartsMessage? message;
        try { message = JsonSerializer.Deserialize<AutoDartsMessage>(json, JsonOptions); }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring malformed AutoDarts message: {Json}", json);
            yield break;
        }

        if (message?.Type != "state" || message.Data is not { } data)
            yield break;

        switch (data.Event)
        {
            case "Throw detected":
                var throws = data.Throws ?? [];
                for (var i = _lastThrows.Count; i < throws.Count; i++)
                    yield return BuildThrowEvent(throws[i]);
                _lastThrows = throws;
                break;

            case "Takeout finished":
                _lastThrows = [];
                yield return new DetectionEvent(DetectionEventType.EndOfTurn, _boardId, null);
                break;

            case "Takeout started":
                break;

            default:
                _logger.LogDebug("Ignoring unrecognized AutoDarts event: {Event}", data.Event);
                break;
        }
    }

    private DetectionEvent BuildThrowEvent(AutoDartsThrow dart)
    {
        var ring = MapRing(dart.Segment.Bed);
        var segment = dart.Segment.Number;
        var position = dart.Coords is { } c ? new BoardPosition(c.X, c.Y) : BoardGeometry.CenterOf(segment, ring);
        var detectedThrow = new DetectedThrow(
            ThrowId: Guid.NewGuid(), Segment: segment, Ring: ring,
            Score: DartScoring.Score(ring, segment), RawNotation: DartScoring.Notation(ring, segment),
            Position: position, Confidence: null, BoardId: _boardId, CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow, Source: DetectionSourceType.AutoDarts);

        return new DetectionEvent(DetectionEventType.Throw, _boardId, detectedThrow);
    }

    // "Double"/"DoubleBull"/"Bull" are unconfirmed against real AutoDarts samples (only SingleOuter,
    // SingleInner, Triple and Outside have been observed) — deliberately not caught, so a wrong guess
    // surfaces immediately via the reconnect loop's exception logging instead of silently mis-scoring.
    private static Ring MapRing(string bed) => bed switch
    {
        "SingleOuter" => Ring.Outer,
        "SingleInner" => Ring.Inner,
        "Triple" => Ring.Triple,
        "Double" => Ring.Double,
        "DoubleBull" => Ring.InnerBull,
        "Bull" => Ring.OuterBull,
        "Outside" => Ring.Miss,
        _ => throw new InvalidOperationException($"Unrecognized AutoDarts bed: '{bed}'"),
    };

    public async ValueTask DisposeAsync()
    {
        // Registered under both its own type and IDetectionSource (DependencyInjection.cs), so the DI
        // container resolves and disposes this same singleton instance twice on shutdown.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await _cts.CancelAsync();
        _channel.Writer.TryComplete();
        try { await _connectionLoop; }
        catch { /* Connection loop observes cancellation internally; nothing actionable on shutdown. */ }

        _cts.Dispose();
    }

    private sealed record AutoDartsMessage([property: JsonPropertyName("type")] string Type, AutoDartsData? Data);

    private sealed record AutoDartsData(
        bool Connected, bool Running, string Status, string Event, int NumThrows, IReadOnlyList<AutoDartsThrow>? Throws);

    private sealed record AutoDartsThrow(AutoDartsSegment Segment, AutoDartsCoords? Coords);

    private sealed record AutoDartsSegment(string Name, int Number, string Bed, int Multiplier);

    private sealed record AutoDartsCoords(double X, double Y);
}
