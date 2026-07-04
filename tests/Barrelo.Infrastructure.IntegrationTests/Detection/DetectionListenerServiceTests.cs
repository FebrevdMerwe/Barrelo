using Barrelo.Application;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.Application.Common.Constants;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
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
/// </summary>
public class DetectionListenerServiceTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private IDispatcher _dispatcher = null!;
    private IGameSessionManager _sessionManager = null!;
    private MockDetectionSource _detectionSource = null!;
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

        _listener = new DetectionListenerService(
            _detectionSource,
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
        var startResult = await _dispatcher.Send(
            new StartMatchCommand(
                "x01",
                [_p1, _p2],
                new Dictionary<string, string> { ["legsToWin"] = "1", ["setsToWin"] = "1" },
                new Dictionary<Guid, int> { [_p1] = 0, [_p2] = 1 }),
            CancellationToken.None);
        startResult.IsError.Should().BeFalse();
        var matchId = startResult.Value.MatchId;

        var detectedThrow = new DetectedThrow(
            ThrowId: Guid.NewGuid(),
            Segment: 20,
            Ring: Ring.Triple,
            Score: DartScoring.Score(Ring.Triple, 20),
            RawNotation: DartScoring.Notation(Ring.Triple, 20),
            Position: BoardGeometry.CenterOf(20, Ring.Triple),
            Confidence: null,
            BoardId: WellKnownBoardIds.Manual,
            CameraIndex: null,
            DetectedAtUtc: DateTimeOffset.UtcNow,
            Source: DetectionSourceType.Mock);

        _detectionSource.SimulateThrow(detectedThrow);

        await WaitUntil(async () =>
        {
            var game = await _sessionManager.TryGetAsync(matchId);
            var state = await game!.GetState();
            return state.RecentThrows.Count == 1;
        });

        var recordedState = await (await _sessionManager.TryGetAsync(matchId))!.GetState();
        recordedState.RecentThrows.Should().ContainSingle();
        recordedState.RecentThrows[0].RawNotation.Should().Be("T20");
        recordedState.RecentThrows[0].Score.Should().Be(60);

        _detectionSource.SimulateEndOfTurn(WellKnownBoardIds.Manual);

        await WaitUntil(async () =>
        {
            var game = await _sessionManager.TryGetAsync(matchId);
            var state = await game!.GetState();
            return state.CurrentPlayerId == _p2;
        });
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
}
