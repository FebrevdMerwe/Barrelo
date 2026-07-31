using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>
/// The host-side half of a client-owned game: an IGame that records what was thrown and interprets none
/// of it. The rules live in the game's browser UI, which replays this log to derive its own state.
///
/// The log is grouped into visits rather than kept flat because that's the shape darts actually has —
/// IGame already models a turn boundary via ReceiveEndOfTurn, and AutoDarts reports a whole visit at a
/// time. Grouping also lets the shell read "the darts thrown this visit" straight off the log instead of
/// reaching into a game-specific payload.
///
/// Deliberately absent: any notion of whose turn it is. Turn order is a rules decision (elimination games
/// skip dead players, "hit a bull, throw again" breaks round-robin), so a visit records only the darts.
/// The current player comes back up from the game as a display hint, never down from here.
/// </summary>
public sealed class ClientOwnedGame(string gameId, int seed, GameSetup setup) : IGame, IClientReportedGame
{
    private const int RecentThrowsShown = 15;

    private readonly List<VisitState> _visits = [];
    private GameResult? _reportedResult;

    public bool IsComplete => _reportedResult is not null;

    public Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct)
    {
        // Visits are created lazily, on the first dart rather than on end-of-turn, so an "open but empty"
        // visit never exists. That's what keeps UndoLastThrow down to two cases.
        if (_visits is not [.., { Ended: false }])
            _visits.Add(new VisitState());

        _visits[^1].Throws.Add(detectedThrow);
        return Task.CompletedTask;
    }

    public Task ReceiveEndOfTurn(CancellationToken ct)
    {
        if (_visits is [.., { Ended: false } open])
            open.Ended = true;

        return Task.CompletedTask;
    }

    public Task UndoLastThrow(CancellationToken ct)
    {
        if (_visits.Count == 0)
            throw new GameRuleViolationException("There is nothing to undo.");

        var last = _visits[^1];
        if (last.Ended)
        {
            // The end-of-turn was the most recent thing to happen, so that's what undo takes back.
            last.Ended = false;
            return Task.CompletedTask;
        }

        last.Throws.RemoveAt(last.Throws.Count - 1);
        if (last.Throws.Count == 0)
            _visits.RemoveAt(_visits.Count - 1);

        return Task.CompletedTask;
    }

    public Task ReportResult(GameResult result, CancellationToken ct)
    {
        _reportedResult = result;
        return Task.CompletedTask;
    }

    public Task<GameStateSnapshot> GetState()
    {
        var allThrows = _visits.SelectMany(v => v.Throws).ToList();
        var recentThrows = allThrows.Count > RecentThrowsShown
            ? allThrows[^RecentThrowsShown..]
            : allThrows;

        return Task.FromResult(new GameStateSnapshot(
            MatchId: Guid.Empty, // Stamped by GameCommandExecutor, which owns the match id.
            GameId: gameId,
            Status: IsComplete ? GameStatus.Complete : GameStatus.InProgress,
            // CurrentPlayerId and the leg/set counters are rules output, which the host by design does not
            // have. Clients get the real values from the game's own barrelo:display message.
            CurrentPlayerId: null,
            LegNumber: 1,
            SetNumber: 1,
            RecentThrows: recentThrows,
            IsComplete: IsComplete,
            WinnerPlayerIds: _reportedResult?.WinnerPlayerIds,
            Payload: new ClientGamePayload(
                seed,
                setup.PlayerIds,
                setup.Options,
                setup.PlayerGroups ?? new Dictionary<Guid, int>(),
                [.. _visits.Select(v => new ClientGameVisit(v.Throws.ToList(), v.Ended))])));
    }

    public Task<GameResult> GetResult() =>
        Task.FromResult(_reportedResult ?? new GameResult([], []));

    private sealed class VisitState
    {
        public List<DetectedThrow> Throws { get; } = [];

        public bool Ended { get; set; }
    }
}

/// <summary>
/// Everything a client-owned game's browser UI needs to derive its own state: who's playing, how the
/// match was configured, and what has been thrown. Rides in GameStateSnapshot.Payload — for this kind of
/// game this <em>is</em> the whole of the state the host holds, so it belongs in the slot reserved for
/// state the host doesn't interpret.
///
/// The roster travels with every push rather than being handed over once at match start, because a client
/// can arrive at any point — a TV switched on mid-match, a tablet refreshed — and must be able to rebuild
/// from a single message with no setup handshake to have missed.
/// </summary>
public sealed record ClientGamePayload(
    int Seed,
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string> Options,
    IReadOnlyDictionary<Guid, int> PlayerGroups,
    IReadOnlyList<ClientGameVisit> Visits);

/// <summary>One player's turn at the board. Open (<c>Ended: false</c>) until the turn boundary arrives;
/// only the final visit in the log can be open.</summary>
public sealed record ClientGameVisit(IReadOnlyList<DetectedThrow> Throws, bool Ended);
