using System.Text.Json;
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
///
/// Connecting, reconnecting and surviving a bad message live in <see cref="WebSocketDetectionSource"/>,
/// shared with every other WebSocket detector.
/// </summary>
public sealed class BoardSimulatorDetectionSource(
    Uri simulatorUri, string boardId, ILogger<BoardSimulatorDetectionSource> logger)
    : WebSocketDetectionSource(simulatorUri, boardId, "board simulator", logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override DetectionSourceType SourceType => DetectionSourceType.Simulator;

    protected override IEnumerable<DetectionEvent> ParseMessage(string json)
    {
        SimulatorMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SimulatorMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Ignoring malformed simulator message: {Json}", json);
            yield break;
        }

        switch (message?.Type)
        {
            case "throw":
                yield return BuildThrowEvent(message);
                break;

            case "endOfTurn":
                yield return new DetectionEvent(DetectionEventType.EndOfTurn, BoardId, null);
                break;
        }
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
            BoardId: BoardId,
            CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow,
            Source: DetectionSourceType.Simulator);

        return new DetectionEvent(DetectionEventType.Throw, BoardId, detectedThrow);
    }

    private sealed record SimulatorMessage(string Type, int? Segment, string? Ring, double? X, double? Y);
}
