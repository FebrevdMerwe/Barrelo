using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>
/// Exercises RemoteGame's IGame behavior against an in-process fake HTTP server (FakeGameServer) standing
/// in for an out-of-process game's RPC endpoint — no Node/Python/external runtime involved, per the
/// project's "keep dotnet test pure-.NET" gate. The real Node reference example
/// (examples/barrelo-remote-game-node) is manually verified instead — see its README checklist.
/// </summary>
public sealed class RemoteGameTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private FakeGameServer _server = null!;
    private Process _dummyProcess = null!;
    private HttpClient _http = null!;

    public Task InitializeAsync()
    {
        _server = new FakeGameServer();
        _server.Start();
        _dummyProcess = SpawnDummyProcess();
        _http = new HttpClient { BaseAddress = new Uri(_server.BaseUrl), Timeout = TimeSpan.FromSeconds(5) };
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _server.DisposeAsync();
        try
        {
            if (!_dummyProcess.HasExited)
                _dummyProcess.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited.
        }
        _dummyProcess.Dispose();
    }

    private static Process SpawnDummyProcess()
    {
        var psi = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("ping", "-n 60 127.0.0.1")
            : new ProcessStartInfo("sleep", "60");
        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        return Process.Start(psi)!;
    }

    private static DetectedThrow SampleThrow() => new(
        ThrowId: Guid.NewGuid(),
        Segment: 20,
        Ring: Ring.Triple,
        Score: 60,
        RawNotation: "T20",
        Position: new BoardPosition(0, 1),
        Confidence: 1.0,
        BoardId: "manual",
        CameraIndex: null,
        DetectedAtUtc: DateTimeOffset.UtcNow,
        Source: DetectionSourceType.Manual);

    private static GameStateSnapshot SampleSnapshot(GameStatus status = GameStatus.InProgress) => new(
        MatchId: Guid.NewGuid(),
        GameId: "fake-game",
        Status: status,
        CurrentPlayerId: Guid.NewGuid(),
        LegNumber: 1,
        SetNumber: 1,
        RecentThrows: [],
        IsComplete: status == GameStatus.Complete,
        WinnerPlayerIds: null,
        Payload: new { note = "hello" });

    private static async Task<T?> ReadJson<T>(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        return string.IsNullOrEmpty(body) ? default : JsonSerializer.Deserialize<T>(body, Json);
    }

    private static async Task WriteJson(HttpListenerContext context, int statusCode, object body)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.SerializeToUtf8Bytes(body, Json);
        await context.Response.OutputStream.WriteAsync(json);
    }

    [Fact]
    public async Task ReceiveThrow_posts_the_detected_throw_as_json()
    {
        DetectedThrow? received = null;
        _server.Routes["POST /throw"] = async ctx =>
        {
            received = await ReadJson<DetectedThrow>(ctx);
            await WriteJson(ctx, 200, new { });
        };

        var game = new RemoteGame(_dummyProcess, _http, NullLogger.Instance);
        var sent = SampleThrow();

        await game.ReceiveThrow(sent, CancellationToken.None);

        received.Should().NotBeNull();
        received!.Segment.Should().Be(sent.Segment);
        received.Ring.Should().Be(sent.Ring);
    }

    [Fact]
    public async Task GetState_deserializes_the_snapshot_including_string_enums()
    {
        var snapshot = SampleSnapshot();
        _server.Routes["GET /state"] = ctx => WriteJson(ctx, 200, snapshot);

        var game = new RemoteGame(_dummyProcess, _http, NullLogger.Instance);

        var result = await game.GetState();

        result.GameId.Should().Be(snapshot.GameId);
        result.Status.Should().Be(GameStatus.InProgress);
        result.CurrentPlayerId.Should().Be(snapshot.CurrentPlayerId);
    }

    [Fact]
    public async Task A_400_response_from_throw_is_translated_into_GameRuleViolationException()
    {
        _server.Routes["POST /throw"] = ctx => WriteJson(ctx, 400, "Game is already complete.");

        var game = new RemoteGame(_dummyProcess, _http, NullLogger.Instance);

        var act = () => game.ReceiveThrow(SampleThrow(), CancellationToken.None);

        await act.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task Connectivity_failure_marks_the_match_aborted_instead_of_throwing()
    {
        var snapshot = SampleSnapshot();
        _server.Routes["GET /state"] = ctx => WriteJson(ctx, 200, snapshot);

        var game = new RemoteGame(_dummyProcess, _http, NullLogger.Instance);

        // One good call first, so RemoteGame has a real last-known snapshot to fall back to.
        var before = await game.GetState();
        before.Status.Should().Be(GameStatus.InProgress);

        _server.StopAbruptly();

        // Neither call should throw — a dead process is reported as Aborted state, not an exception.
        await game.ReceiveThrow(SampleThrow(), CancellationToken.None);
        var after = await game.GetState();

        after.Status.Should().Be(GameStatus.Aborted);
        after.MatchId.Should().Be(snapshot.MatchId);
    }

    [Fact]
    public async Task GetResult_deserializes_correctly()
    {
        var winnerId = Guid.NewGuid();
        _server.Routes["GET /result"] = ctx => WriteJson(ctx, 200, new GameResult([winnerId], [winnerId]));

        var game = new RemoteGame(_dummyProcess, _http, NullLogger.Instance);

        var result = await game.GetResult();

        result.WinnerPlayerIds.Should().ContainSingle().Which.Should().Be(winnerId);
    }
}
