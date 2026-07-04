using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>
/// Connects to the standalone Barrelo.BoardSimulator app over WebSocket, standing in for a real detector
/// (e.g. a future ThirdPartyDetectionSource) behind the same IDetectionSource contract. The simulator's wire
/// protocol is our own invention (not a guess at the third-party detector's undocumented API) — a tiny JSON envelope:
/// {"type":"throw","segment":20,"ring":"Triple","x":0.13,"y":0.91} or {"type":"endOfTurn"}. x/y are the
/// real SVG click position when available (omitted for keyboard activation or the Miss button), and fall
/// back to a deterministic BoardGeometry-fabricated center point when absent.
/// </summary>
public sealed class BoardSimulatorDetectionSource : IDetectionSource, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    private readonly Uri _simulatorUri;
    private readonly string _boardId;
    private readonly ILogger<BoardSimulatorDetectionSource> _logger;
    private readonly Channel<DetectionEvent> _channel = Channel.CreateUnbounded<DetectionEvent>();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _connectionLoop;

    private volatile ClientWebSocket? _socket;
    private int _disposed;

    public BoardSimulatorDetectionSource(Uri simulatorUri, string boardId, ILogger<BoardSimulatorDetectionSource> logger)
    {
        _simulatorUri = simulatorUri;
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
                await socket.ConnectAsync(_simulatorUri, ct);
                _socket = socket;
                _logger.LogInformation("Connected to board simulator at {Uri}", _simulatorUri);
                backoff = InitialBackoff;

                await ReceiveLoop(socket, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Board simulator connection lost; reconnecting in {Delay}", backoff);
            }
            finally
            {
                _socket = null;
            }

            if (ct.IsCancellationRequested)
                break;

            try
            {
                await Task.Delay(backoff, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

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
                    _logger.LogWarning("Board simulator closed the connection.");
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            var evt = TryParse(json);
            if (evt is not null)
                _channel.Writer.TryWrite(evt);
        }
    }

    private DetectionEvent? TryParse(string json)
    {
        SimulatorMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SimulatorMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring malformed simulator message: {Json}", json);
            return null;
        }

        return message?.Type switch
        {
            "throw" => BuildThrowEvent(message),
            "endOfTurn" => new DetectionEvent(DetectionEventType.EndOfTurn, _boardId, null),
            _ => null,
        };
    }

    private DetectionEvent BuildThrowEvent(SimulatorMessage message)
    {
        var ring = Enum.Parse<Ring>(message.Ring!, ignoreCase: true);
        var segment = message.Segment ?? 0;
        var position = message.X is { } x && message.Y is { } y
            ? new BoardPosition(x, y)
            : BoardGeometry.CenterOf(segment, ring);
        var detectedThrow = new DetectedThrow(
            ThrowId: Guid.NewGuid(),
            Segment: segment,
            Ring: ring,
            Score: DartScoring.Score(ring, segment),
            RawNotation: DartScoring.Notation(ring, segment),
            Position: position,
            Confidence: null,
            BoardId: _boardId,
            CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow,
            Source: DetectionSourceType.Simulator);

        return new DetectionEvent(DetectionEventType.Throw, _boardId, detectedThrow);
    }

    public async ValueTask DisposeAsync()
    {
        // Registered under both its own type and IDetectionSource (DependencyInjection.cs), so the DI
        // container resolves and disposes this same singleton instance twice on shutdown.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await _cts.CancelAsync();
        _channel.Writer.TryComplete();
        try
        {
            await _connectionLoop;
        }
        catch
        {
            // Connection loop observes cancellation internally; nothing actionable on shutdown.
        }

        _cts.Dispose();
    }

    private sealed record SimulatorMessage(string Type, int? Segment, string? Ring, double? X, double? Y);
}
