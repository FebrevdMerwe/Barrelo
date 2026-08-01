using Barrelo.Application;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.Application.Common.Constants;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Application.Common.Notifications;
using Barrelo.Domain.Entities;
using Barrelo.GameSdk;
using Barrelo.Games.X01;
using Barrelo.Infrastructure.External.Detection;
using Barrelo.Infrastructure.External.GamePlugins;
using Barrelo.Infrastructure.External.Notifications;
using Barrelo.Infrastructure.External.Sessions;
using Barrelo.Infrastructure.Persistence;
using Barrelo.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Barrelo.Infrastructure.IntegrationTests.Detection;

/// <summary>
/// Before this service existed, no streaming IDetectionSource had a consumer anywhere in the running app —
/// only the manual REST path actually drove gameplay. This proves DetectionListenerService is that missing
/// consumer: a real match, driven end-to-end purely by pushing events onto a MockDetectionSource.
///
/// The later tests here are about the listener surviving a board that goes away and comes back, which is
/// the failure that used to strand a match until the whole app was restarted.
/// </summary>
public class DetectionListenerServiceTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private IDispatcher _dispatcher = null!;
    private IGameSessionManager _sessionManager = null!;
    private MockDetectionSource _detectionSource = null!;
    private RecordingStatusNotifier _statusNotifier = null!;
    private DetectionListenerService _listener = null!;
    private Guid _p1;
    private Guid _p2;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var context = _database.CreateContext();
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddDartsDispatcher();
        services.AddSingleton(context);
        services.AddSingleton<IPlayerRepository, PlayerRepository>();
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<ISessionPlayerStore, SessionPlayerStore>();
        services.AddSingleton<ISessionLeaderboardStore, SessionLeaderboardStore>();
        services.AddSingleton<IGameCatalog>(new GameCatalog([new X01GameFactory()]));
        services.AddSingleton<IGameNotifier, NullGameNotifier>();

        _detectionSource = new MockDetectionSource();
        services.AddSingleton<IDetectionSource>(_detectionSource);

        var provider = services.BuildServiceProvider();
        _dispatcher = provider.GetRequiredService<IDispatcher>();
        _sessionManager = provider.GetRequiredService<IGameSessionManager>();

        var playerRepository = provider.GetRequiredService<IPlayerRepository>();
        var p1 = Player.Create("P1").Value;
        var p2 = Player.Create("P2").Value;
        await playerRepository.Add(p1, CancellationToken.None);
        await playerRepository.Add(p2, CancellationToken.None);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        _p1 = p1.Id;
        _p2 = p2.Id;

        _statusNotifier = new RecordingStatusNotifier();
        _listener = new DetectionListenerService(
            _detectionSource,
            _statusNotifier,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<DetectionListenerService>.Instance);
        await _listener.StartAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _listener.StopAsync(CancellationToken.None);
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Streamed_throw_and_end_of_turn_events_drive_a_real_match()
    {
        var matchId = await StartMatch();

        _detectionSource.SimulateThrow(Dart(20, Ring.Triple));

        await WaitUntil(async () => (await StateOf(matchId)).RecentThrows.Count == 1);

        var recordedState = await StateOf(matchId);
        recordedState.RecentThrows.Should().ContainSingle();
        recordedState.RecentThrows[0].RawNotation.Should().Be("T20");
        recordedState.RecentThrows[0].Score.Should().Be(60);

        _detectionSource.SimulateEndOfTurn(WellKnownBoardIds.Manual);

        await WaitUntil(async () => (await StateOf(matchId)).CurrentPlayerId == _p2);
    }

    /// <summary>
    /// The reconnect failure that stalled whole evenings. A board manager that drops and comes back can
    /// lose the takeout that happened in between: the next thing the host sees is a visit with *fewer*
    /// darts on the board and no turn boundary in between. The game still had P1 at the oche on a full
    /// visit, so P2's darts were rejected one after another as fourth darts and the match never moved
    /// again — no error on screen, just a board that had stopped counting.
    /// </summary>
    [Fact]
    public async Task A_takeout_lost_in_a_reconnect_gap_does_not_stall_the_match()
    {
        var matchId = await StartMatch();

        // P1's visit, as AutoDarts reports it: the whole visit resent on every dart.
        var t20 = Dart(20, Ring.Triple);
        SimulateVisit(t20);
        SimulateVisit(t20, t20);
        SimulateVisit(t20, t20, t20);
        await WaitUntil(async () => (await StateOf(matchId)).RecentThrows.Count == 3);

        // Socket drops, P1 pulls the darts, socket comes back — the "Takeout finished" event was never
        // delivered. The first thing seen after the gap is the next visit's opening dart.
        SimulateVisit(Dart(5, Ring.OuterSingle));

        await WaitUntil(async () => (await StateOf(matchId)).CurrentPlayerId == _p2);

        var state = await StateOf(matchId);
        state.RecentThrows.Should().HaveCount(4);
        state.RecentThrows[3].RawNotation.Should().Be(DartScoring.Notation(Ring.OuterSingle, 5));
    }

    [Fact]
    public async Task A_visit_resent_after_a_reconnect_is_not_recorded_twice()
    {
        var matchId = await StartMatch();

        var t20 = Dart(20, Ring.Triple);
        SimulateVisit(t20);
        SimulateVisit(t20, t20);
        await WaitUntil(async () => (await StateOf(matchId)).RecentThrows.Count == 2);

        // Reconnect mid-visit: the board manager states what is currently on the board, which is the same
        // two darts. Nothing here is new, and the turn is very much not over.
        SimulateVisit(t20, t20);
        SimulateVisit(t20, t20, Dart(5, Ring.OuterSingle));

        await WaitUntil(async () => (await StateOf(matchId)).RecentThrows.Count == 3);

        var state = await StateOf(matchId);
        state.RecentThrows.Should().HaveCount(3);
        state.CurrentPlayerId.Should().Be(_p1);
    }

    /// <summary>An event the listener cannot handle used to escape ExecuteAsync, which faults the
    /// BackgroundService — by default taking the host with it, and otherwise leaving a board that silently
    /// scores nothing. One bad event must cost one event.</summary>
    [Fact]
    public async Task An_event_the_listener_cannot_handle_does_not_stop_the_stream()
    {
        var matchId = await StartMatch();

        _detectionSource.Simulate(new DetectionEvent((DetectionEventType)999, WellKnownBoardIds.Manual, null));
        _detectionSource.SimulateThrow(Dart(20, Ring.Triple));

        await WaitUntil(async () => (await StateOf(matchId)).RecentThrows.Count == 1);
    }

    [Fact]
    public async Task Connection_changes_are_pushed_to_watching_screens()
    {
        _detectionSource.Simulate(DetectionEvent.ConnectionChanged(WellKnownBoardIds.Manual, false));
        await WaitUntil(() => Task.FromResult(_statusNotifier.Statuses.Count == 1));

        _detectionSource.Simulate(DetectionEvent.ConnectionChanged(WellKnownBoardIds.Manual, true));
        await WaitUntil(() => Task.FromResult(_statusNotifier.Statuses.Count == 2));

        _statusNotifier.Statuses[0].Should().Be(new DetectionStatus(DetectionSourceType.Mock, false));
        _statusNotifier.Statuses[1].Should().Be(new DetectionStatus(DetectionSourceType.Mock, true));
    }

    private void SimulateVisit(params DetectedThrow[] darts) =>
        _detectionSource.Simulate(
            new DetectionEvent(DetectionEventType.VisitUpdated, WellKnownBoardIds.Manual, null, darts));

    private static DetectedThrow Dart(int segment, Ring ring) => new(
        ThrowId: Guid.NewGuid(),
        Segment: segment,
        Ring: ring,
        Score: DartScoring.Score(ring, segment),
        RawNotation: DartScoring.Notation(ring, segment),
        Position: BoardGeometry.CenterOf(segment, ring),
        Confidence: null,
        BoardId: WellKnownBoardIds.Manual,
        CameraIndex: null,
        DetectedAtUtc: DateTimeOffset.UtcNow,
        Source: DetectionSourceType.Mock);

    private async Task<Guid> StartMatch()
    {
        var startResult = await _dispatcher.Send(
            new StartMatchCommand(
                "x01",
                [_p1, _p2],
                new Dictionary<string, string> { ["legsToWin"] = "1", ["setsToWin"] = "1" },
                new Dictionary<Guid, int> { [_p1] = 0, [_p2] = 1 }),
            CancellationToken.None);
        startResult.IsError.Should().BeFalse();
        return startResult.Value.MatchId;
    }

    private async Task<GameStateSnapshot> StateOf(Guid matchId)
    {
        var game = await _sessionManager.TryGetAsync(matchId);
        return await game!.GetState();
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(20);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    private sealed class RecordingStatusNotifier : IDetectionStatusNotifier
    {
        private readonly List<DetectionStatus> _statuses = [];

        public IReadOnlyList<DetectionStatus> Statuses
        {
            get { lock (_statuses) return _statuses.ToList(); }
        }

        public Task NotifyStatusChanged(DetectionStatus status, CancellationToken ct)
        {
            lock (_statuses) _statuses.Add(status);
            return Task.CompletedTask;
        }
    }
}
