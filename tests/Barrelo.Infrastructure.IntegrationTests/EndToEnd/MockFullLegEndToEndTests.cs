using Barrelo.Application;
using Barrelo.Application.Commands.Detection.RecordDetectedThrow;
using Barrelo.Application.Commands.Matches.StartMatch;
using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.Interfaces.Persistence;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.Domain.Entities;
using Barrelo.GameSdk;
using Barrelo.Games.X01;
using Barrelo.Infrastructure.External.GamePlugins;
using Barrelo.Infrastructure.External.Notifications;
using Barrelo.Infrastructure.External.Sessions;
using Barrelo.Infrastructure.Persistence;
using Barrelo.Infrastructure.Persistence.Repositories;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Barrelo.Infrastructure.IntegrationTests.EndToEnd;

/// <summary>
/// TASKS.md's primary Phase-1 correctness gate: a full mock 501 leg driven entirely through the real
/// dispatcher/commands, real SQLite-backed permanent players, and the real X01 rules engine (referenced
/// directly here for speed/determinism — ALC-loaded plugin mechanics are covered separately by
/// PluginGameLoaderTests). Asserts the final GameStateSnapshot reaches a winner.
/// </summary>
public class MockFullLegEndToEndTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private IDispatcher _dispatcher = null!;
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

        var provider = services.BuildServiceProvider();
        _dispatcher = provider.GetRequiredService<IDispatcher>();

        var playerRepository = provider.GetRequiredService<IPlayerRepository>();
        var p1 = Player.Create("P1").Value;
        var p2 = Player.Create("P2").Value;
        await playerRepository.Add(p1, CancellationToken.None);
        await playerRepository.Add(p2, CancellationToken.None);
        await provider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken.None);
        _p1 = p1.Id;
        _p2 = p2.Id;
    }

    public Task DisposeAsync() => _database.DisposeAsync();

    [Fact]
    public async Task Full_mock_501_leg_reaches_a_winner()
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

        // P1 visit 1: 501 -> 321
        await Throw(20, Ring.Triple);
        await Throw(20, Ring.Triple);
        await Throw(20, Ring.Triple);

        // P2 visit 1: all misses, irrelevant to who wins
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);

        // P1 visit 2: 321 -> 141
        await Throw(20, Ring.Triple);
        await Throw(20, Ring.Triple);
        await Throw(20, Ring.Triple);

        // P2 visit 2
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);

        // P1 visit 3: 141 -> 21 -> 20 (deliberately not a checkout dart, to set up the finish next visit)
        await Throw(20, Ring.Triple);
        await Throw(20, Ring.Triple);
        var afterVisit3 = await Throw(1, Ring.Inner);
        afterVisit3.Payload.Should().NotBeNull();

        // P2 visit 3
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);

        // P1 visit 4: 20 -> 0 via Double10, a valid double-out checkout -> leg (and match) won
        var finalState = await Throw(10, Ring.Double);

        finalState.IsComplete.Should().BeTrue();
        finalState.WinnerPlayerIds.Should().BeEquivalentTo([_p1]);
        finalState.Status.Should().Be(GameStatus.Complete);
        finalState.MatchId.Should().Be(matchId);

        async Task<Barrelo.Application.Common.GameExecution.MatchStateSnapshotDto> Throw(int segment, Ring ring)
        {
            var result = await _dispatcher.Send(new RecordDetectedThrowCommand(segment, ring), CancellationToken.None);
            result.IsError.Should().BeFalse(because: string.Join(", ", result.ErrorsOrEmptyList.Select(e => e.Description)));
            return result.Value;
        }
    }
}
