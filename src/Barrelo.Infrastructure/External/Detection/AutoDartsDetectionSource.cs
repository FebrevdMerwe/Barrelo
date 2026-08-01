using System.Text.Json;
using System.Text.Json.Serialization;
using Barrelo.Application.Common.Notifications;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>
/// Connects to a local AutoDarts board-manager event stream (ws://host:3180/api/events) and maps its
/// per-visit "state" events onto the canonical, detector-agnostic DetectedThrow. Connecting, reconnecting
/// and surviving a bad message are all <see cref="WebSocketDetectionSource"/>'s job — this type is nothing
/// but the mapping.
///
/// AutoDarts sends the *cumulative* set of darts for the current visit on every "Throw detected" event
/// (numThrows/throws grows to up to 3), not one message per dart. This adapter passes that through as-is,
/// as a VisitUpdated event carrying the whole visit — working out which darts are new is
/// DetectionListenerService's VisitDiffer's job. Keeping the adapter stateless is deliberate: a
/// reconnect-and-resend then self-corrects, whereas an adapter holding its own "darts emitted so far"
/// count silently swallowed a whole visit whenever a reconnect straddled a takeout.
///
/// Turn boundary is the "Takeout finished" event (board cleared, numThrows back to 0) rather than
/// "Takeout started", so EndOfTurn only fires once the visit is fully wrapped up.
/// </summary>
public sealed class AutoDartsDetectionSource(Uri eventsUri, string boardId, ILogger<AutoDartsDetectionSource> logger)
    : WebSocketDetectionSource(eventsUri, boardId, "AutoDarts board manager", logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override DetectionSourceType SourceType => DetectionSourceType.AutoDarts;

    protected override IEnumerable<DetectionEvent> ParseMessage(string json)
    {
        AutoDartsMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<AutoDartsMessage>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Logger.LogWarning(ex, "Ignoring malformed AutoDarts message: {Json}", json);
            yield break;
        }

        if (message?.Type != "state" || message.Data is not { } data)
            yield break;

        switch (data.Event)
        {
            case "Throw detected":
                var visit = (data.Throws ?? []).Select(BuildThrow).ToList();
                yield return new DetectionEvent(DetectionEventType.VisitUpdated, BoardId, null, visit);
                break;

            case "Takeout finished":
                yield return new DetectionEvent(DetectionEventType.EndOfTurn, BoardId, null);
                break;

            case "Takeout started":
                break;

            default:
                Logger.LogDebug("Ignoring unrecognized AutoDarts event: {Event}", data.Event);
                break;
        }
    }

    private DetectedThrow BuildThrow(AutoDartsThrow dart)
    {
        var segment = dart.Segment.Number;
        var ring = MapRing(dart.Segment.Bed, segment);
        var position = dart.Coords is { } c ? new BoardPosition(c.X, c.Y) : BoardGeometry.CenterOf(segment, ring);

        return new DetectedThrow(
            ThrowId: Guid.NewGuid(), Segment: segment, Ring: ring,
            Score: DartScoring.Score(ring, segment), RawNotation: DartScoring.Notation(ring, segment),
            Position: position, Confidence: null, BoardId: BoardId, CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow, Source: DetectionSourceType.AutoDarts);
    }

    // Confirmed against real AutoDarts samples: bull hits arrive as Bed "Single"/"Double" with
    // Number 25 (Barrelo.GameSdk.Ring.Single/Double), same as any other segment — not the literal
    // "Bull"/"DoubleBull" strings this mapping used to (wrongly) guess. Wedge doubles at ("Double",
    // 1-20) remain unconfirmed against a real sample, so any other unrecognized bed/number pair is
    // deliberately not caught, letting a wrong guess surface immediately rather than silently
    // mis-scoring. It now costs only the offending message — the base class logs the payload and keeps
    // the socket up, where this used to drop the connection and the darts around it.
    private static Ring MapRing(string bed, int number) => (bed, number) switch
    {
        ("SingleOuter", _) => Ring.OuterSingle,
        ("SingleInner", _) => Ring.InnerSingle,
        ("Triple", _) => Ring.Triple,
        ("Double", _) => Ring.Double,
        ("Single", 25) => Ring.Single,
        ("Outside", _) => Ring.Miss,
        _ => throw new InvalidOperationException($"Unrecognized AutoDarts bed/number: '{bed}'/{number}"),
    };

    private sealed record AutoDartsMessage([property: JsonPropertyName("type")] string Type, AutoDartsData? Data);

    private sealed record AutoDartsData(
        bool Connected, bool Running, string? Status, string? Event, int NumThrows, IReadOnlyList<AutoDartsThrow>? Throws);

    private sealed record AutoDartsThrow(AutoDartsSegment Segment, AutoDartsCoords? Coords);

    private sealed record AutoDartsSegment(string Name, int Number, string Bed, int Multiplier);

    private sealed record AutoDartsCoords(double X, double Y);
}
