using Darts.Application;
using Darts.Application.Commands.Detection.RecordDetectedThrow;
using Darts.Application.Commands.Matches.StartMatch;
using Darts.Application.Common.Dispatch;
using Darts.Application.Common.Interfaces.Persistence;
using Darts.Application.Common.Interfaces.Services;
using Darts.Domain.Entities;
using Darts.GameSdk;
using Darts.Games.X01;
using Darts.Infrastructure.External.GamePlugins;
using Darts.Infrastructure.External.Notifications;
using Darts.Infrastructure.Persistence;
using Darts.Infrastructure.Persistence.Repositories;
using ErrorOr;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using InputSource = Darts.Domain.Enums.InputSource;

namespace Darts.Infrastructure.IntegrationTests.EndToEnd;

/// <summary>
/// TASKS.md's primary Phase-1 correctness gate: a full mock 501 leg driven entirely through the real
/// dispatcher/commands, a real SQLite-backed match repository, and the real X01 rules engine (referenced
/// directly here for speed/determinism — ALC-loaded plugin mechanics are covered separately by
/// PluginGameLoaderTests). Asserts the final GameStateSnapshot and the persisted ThrowRecords.
/// </summary>
public class MockFullLegEndToEndTests : IAsyncLifetime
{
    private readonly SqliteTestDatabase _database = new();
    private IDispatcher _dispatcher = null!;
    private IMatchRepository _matchRepository = null!;
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
        services.AddSingleton<IMatchRepository, MatchRepository>();
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IGameSessionManager, GameSessionManager>();
        services.AddSingleton<IGameCatalog>(new GameCatalog([new X01GameFactory()]));
        services.AddSingleton<IGameNotifier, NullGameNotifier>();

        var provider = services.BuildServiceProvider();
        _dispatcher = provider.GetRequiredService<IDispatcher>();
        _matchRepository = provider.GetRequiredService<IMatchRepository>();

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
    public async Task Full_mock_501_leg_persists_every_throw_and_reaches_a_winner()
    {
        var startResult = await _dispatcher.Send(
            new StartMatchCommand(
                "x01",
                [_p1, _p2],
                new Dictionary<string, string> { ["legsToWin"] = "1", ["setsToWin"] = "1" },
                InputSource.Manual),
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
        finalState.WinnerPlayerId.Should().Be(_p1);
        finalState.Status.Should().Be(GameStatus.Complete);

        var records = await _matchRepository.GetThrowRecords(matchId, CancellationToken.None);
        records.Should().HaveCount(19); // 10 darts for P1 (3+3+3+1) + 9 for P2 (3+3+3)
        records.Select(r => r.Sequence).Should().Equal(Enumerable.Range(1, 19));
        records.Last().RawNotation.Should().Be("D10");
        records.Last().PlayerId.Should().Be(_p1);
        records.Where(r => r.PlayerId == _p1).Should().HaveCount(10);
        records.Where(r => r.PlayerId == _p2).Should().HaveCount(9);

        async Task<GameStateSnapshot> Throw(int segment, Ring ring)
        {
            var result = await _dispatcher.Send(new RecordDetectedThrowCommand(null, segment, ring), CancellationToken.None);
            result.IsError.Should().BeFalse(because: string.Join(", ", result.ErrorsOrEmptyList.Select(e => e.Description)));
            return result.Value;
        }
    }
}
