using Barrelo.GameSdk;

namespace Barrelo.Games.X01;

/// <summary>
/// Reference IGame implementation for classic X01 (default 501). Undo works by replaying the
/// full append-only event log from scratch rather than reversing deltas — the hard cases (undoing
/// the dart that completed a leg/set, or undoing back into a reverted bust visit) all fall out of
/// the same code path this way instead of needing special-cased reversal logic.
///
/// Players may share a group (team play): a group has one cumulative score/legs/sets, like real
/// doubles/teams darts. Turn order stays a flat round-robin over <see cref="_players"/> regardless
/// of grouping — <see cref="AdvanceToNextPlayer"/> is unchanged from the ungrouped implementation —
/// only which group's shared score a throw affects depends on <see cref="_groupByPlayer"/>.
/// </summary>
public sealed class X01Game : IGame
{
    /// <summary>
    /// A visit holds at most this many darts, but reaching it does not end the visit — only an explicit
    /// EndOfTurn does. Turn order follows the physical takeout, which is what a detection source reports,
    /// so the throwing player stays on the oche until the darts actually leave the board.
    /// </summary>
    private const int DartsPerVisit = 3;

    private enum LogEntryKind { Throw, EndOfTurn }

    private sealed record LogEntry(LogEntryKind Kind, DetectedThrow? Throw);

    private readonly IReadOnlyList<Guid> _players;
    private readonly IReadOnlyDictionary<Guid, int> _groupByPlayer;
    private readonly X01Options _options;
    private readonly List<LogEntry> _log = [];

    // Derived state, fully recomputed by Rebuild() after every log mutation.
    private Dictionary<int, X01GroupState> _groupStates = [];
    private int _currentPlayerIndex;
    private int _legNumber = 1;
    private int _setNumber = 1;
    private int _legsPlayedTotal;
    private List<DetectedThrow> _currentVisitThrows = [];
    private List<DetectedThrow> _lastEndedVisitThrows = [];
    private List<DetectedThrow> _currentLegThrows = [];
    private bool _isComplete;
    private IReadOnlyList<Guid>? _winnerPlayerIds;

    internal X01Game(IReadOnlyList<Guid> players, IReadOnlyDictionary<Guid, int> groupByPlayer, X01Options options)
    {
        _players = players;
        _groupByPlayer = groupByPlayer;
        _options = options;
        Rebuild();
    }

    public bool IsComplete => _isComplete;

    public Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct)
    {
        EnsureNotComplete();
        EnsureVisitHasRoom();
        _log.Add(new LogEntry(LogEntryKind.Throw, detectedThrow));
        Rebuild();
        return Task.CompletedTask;
    }

    public Task ReceiveEndOfTurn(CancellationToken ct)
    {
        EnsureNotComplete();
        _log.Add(new LogEntry(LogEntryKind.EndOfTurn, null));
        Rebuild();
        return Task.CompletedTask;
    }

    public Task UndoLastThrow(CancellationToken ct)
    {
        if (_log.Count == 0)
            throw new GameRuleViolationException("There is nothing to undo.");

        _log.RemoveAt(_log.Count - 1);
        Rebuild();
        return Task.CompletedTask;
    }

    public Task<GameStateSnapshot> GetState()
    {
        var payload = new X01StatePayload(
            _groupStates.Values
                .OrderBy(g => g.GroupIndex)
                .Select(g => new X01GroupScore(g.GroupIndex, g.MemberPlayerIds, g.RemainingScore, g.LegsWon, g.SetsWon))
                .ToArray(),
            _currentVisitThrows.ToArray(),
            _lastEndedVisitThrows.ToArray());

        var snapshot = new GameStateSnapshot(
            MatchId: Guid.Empty, // the plugin doesn't know its own MatchId; the host stamps it in
            GameId: X01GameFactory.GameId,
            Status: _isComplete ? GameStatus.Complete : GameStatus.InProgress,
            CurrentPlayerId: _isComplete ? null : _players[_currentPlayerIndex],
            LegNumber: _legNumber,
            SetNumber: _setNumber,
            RecentThrows: _currentLegThrows.ToArray(),
            IsComplete: _isComplete,
            WinnerPlayerIds: _winnerPlayerIds,
            Payload: payload);

        return Task.FromResult(snapshot);
    }

    public Task<GameResult> GetResult()
    {
        if (!_isComplete)
            throw new GameRuleViolationException("The game is not complete yet.");

        var standings = _groupStates.Values
            .OrderByDescending(g => g.SetsWon)
            .ThenByDescending(g => g.LegsWon)
            .SelectMany(g => g.MemberPlayerIds)
            .ToArray();

        return Task.FromResult(new GameResult(_winnerPlayerIds!, standings));
    }

    private void EnsureNotComplete()
    {
        if (_isComplete)
            throw new GameRuleViolationException("The game has already finished.");
    }

    /// <summary>
    /// Nothing ends a full visit on dart count any more, so a fourth dart would otherwise keep scoring
    /// for a player whose three darts are already in the board — and stay invisible, since the visit is
    /// only ever displayed three darts wide. Refusing it surfaces the missing takeout instead.
    /// </summary>
    private void EnsureVisitHasRoom()
    {
        if (_currentVisitThrows.Count >= DartsPerVisit)
            throw new GameRuleViolationException(
                "This visit already has three darts — end the turn before throwing again.");
    }

    private void Rebuild()
    {
        var distinctGroups = _players.Select(id => _groupByPlayer[id]).Distinct();
        _groupStates = distinctGroups.ToDictionary(
            g => g,
            g => new X01GroupState(g, _players.Where(id => _groupByPlayer[id] == g).ToArray(), _options.StartingScore));

        _currentPlayerIndex = 0;
        _legNumber = 1;
        _setNumber = 1;
        _legsPlayedTotal = 0;
        _currentVisitThrows = [];
        _currentLegThrows = [];
        _isComplete = false;
        _winnerPlayerIds = null;

        var visitStartRemaining = _options.StartingScore;

        // A visit ends on an explicit EndOfTurn (from the UI or a detection source), or early when the
        // rules say so — a bust or a checkout, neither of which waits for the remaining darts. Dart
        // count alone never ends one. Detection sources (e.g. AutoDarts) fire EndOfTurn unconditionally
        // once the board is cleared, including for a visit a bust or checkout already ended — so an
        // EndOfTurn immediately following one of those must be a no-op, or the player would get
        // advanced twice for the same physical turn.
        var turnAlreadyEnded = false;
        List<DetectedThrow> lastEndedVisitThrows = [];
        var justEndedVisit = false;

        for (var i = 0; i < _log.Count; i++)
        {
            var entry = _log[i];

            if (entry.Kind == LogEntryKind.EndOfTurn)
            {
                if (!turnAlreadyEnded)
                {
                    lastEndedVisitThrows = _currentVisitThrows;
                    justEndedVisit = true;
                    AdvanceToNextPlayer();
                    _currentVisitThrows = [];
                }
                turnAlreadyEnded = false;
                continue;
            }

            justEndedVisit = false;
            turnAlreadyEnded = false;

            var detectedThrow = entry.Throw!;
            var throwingPlayerId = _players[_currentPlayerIndex];
            var group = _groupStates[_groupByPlayer[throwingPlayerId]];

            if (_currentVisitThrows.Count == 0)
                visitStartRemaining = group.RemainingScore;

            _currentVisitThrows.Add(detectedThrow);
            _currentLegThrows.Add(detectedThrow);

            var newRemaining = group.RemainingScore - detectedThrow.Score;
            var isBust = newRemaining < 0
                || (_options.DoubleOut && newRemaining == 1)
                || (newRemaining == 0 && _options.DoubleOut && !DartScoring.IsValidCheckoutRing(detectedThrow.Ring));

            if (isBust)
            {
                group.RemainingScore = visitStartRemaining;
                lastEndedVisitThrows = _currentVisitThrows;
                justEndedVisit = true;
                turnAlreadyEnded = true;
                AdvanceToNextPlayer();
                _currentVisitThrows = [];
                continue;
            }

            group.RemainingScore = newRemaining;

            if (newRemaining == 0)
            {
                lastEndedVisitThrows = _currentVisitThrows;
                justEndedVisit = true;
                turnAlreadyEnded = true;
                WinLeg(group);
                if (_isComplete) break;
                continue;
            }
        }

        _lastEndedVisitThrows = justEndedVisit ? lastEndedVisitThrows : [];
    }

    private void WinLeg(X01GroupState group)
    {
        group.LegsWon++;
        _legsPlayedTotal++;
        _currentVisitThrows = [];
        _currentLegThrows = [];

        if (group.LegsWon >= _options.LegsToWin)
        {
            group.SetsWon++;

            if (group.SetsWon >= _options.SetsToWin)
            {
                _isComplete = true;
                _winnerPlayerIds = group.MemberPlayerIds;
                return;
            }

            foreach (var state in _groupStates.Values)
                state.LegsWon = 0;

            _setNumber++;
            _legNumber = 1;
        }
        else
        {
            _legNumber++;
        }

        foreach (var state in _groupStates.Values)
            state.RemainingScore = _options.StartingScore;

        _currentPlayerIndex = _legsPlayedTotal % _players.Count;
    }

    private void AdvanceToNextPlayer()
    {
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
    }
}
