using Barrelo.GameSdk;

namespace Barrelo.Games.Cricket;

/// <summary>
/// Reference second IGame implementation: Standard Cricket, single leg, N groups. Like X01Game,
/// undo works by replaying the full append-only event log from scratch rather than reversing
/// deltas — undoing the dart that closed the match, or that scored the winning points, falls out
/// of the same code path this way instead of needing special-cased reversal logic.
///
/// Players may share a group (team play): a group has one cumulative set of marks/points. Turn
/// order stays a flat round-robin over <see cref="_players"/> regardless of grouping — only which
/// group's shared marks/points a throw affects depends on <see cref="_groupByPlayer"/>.
///
/// A group wins by closing all 7 targets (15-20 + Bull, each needing 3 marks) while holding
/// strictly more points than every other group. Closing everything while tied or behind does not
/// end the match: already-closed numbers that some other group hasn't closed yet keep scoring for
/// the group chasing the lead, until it either takes the lead outright or another group closes out
/// and overtakes it.
/// </summary>
public sealed class CricketGame : IGame
{
    private enum LogEntryKind { Throw, EndOfTurn }

    private sealed record LogEntry(LogEntryKind Kind, DetectedThrow? Throw);

    private readonly IReadOnlyList<Guid> _players;
    private readonly IReadOnlyDictionary<Guid, int> _groupByPlayer;
    private readonly List<LogEntry> _log = [];

    // Derived state, fully recomputed by Rebuild() after every log mutation.
    private Dictionary<int, CricketGroupState> _groupStates = [];
    private int _currentPlayerIndex;
    private List<DetectedThrow> _currentVisitThrows = [];
    private List<DetectedThrow> _allThrows = [];
    private bool _isComplete;
    private IReadOnlyList<Guid>? _winnerPlayerIds;

    internal CricketGame(IReadOnlyList<Guid> players, IReadOnlyDictionary<Guid, int> groupByPlayer)
    {
        _players = players;
        _groupByPlayer = groupByPlayer;
        Rebuild();
    }

    public bool IsComplete => _isComplete;

    public Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct)
    {
        EnsureNotComplete();
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
        var payload = new CricketStatePayload(
            _groupStates.Values
                .OrderBy(g => g.GroupIndex)
                .Select(g => new CricketGroupScore(g.GroupIndex, g.MemberPlayerIds, g.Marks, g.Points, g.ClosedCount))
                .ToArray(),
            _currentVisitThrows.ToArray());

        var snapshot = new GameStateSnapshot(
            MatchId: Guid.Empty, // the plugin doesn't know its own MatchId; the host stamps it in
            GameId: CricketGameFactory.GameId,
            Status: _isComplete ? GameStatus.Complete : GameStatus.InProgress,
            CurrentPlayerId: _isComplete ? null : _players[_currentPlayerIndex],
            LegNumber: 1, // Cricket is single-leg — the whole match is the one leg
            SetNumber: 1,
            RecentThrows: _allThrows.ToArray(),
            IsComplete: _isComplete,
            WinnerPlayerIds: _winnerPlayerIds,
            Payload: payload);

        return Task.FromResult(snapshot);
    }

    public Task<GameResult> GetResult()
    {
        if (!_isComplete)
            throw new GameRuleViolationException("The game is not complete yet.");

        // Progress-based ranking for non-winning groups — the Cricket analogue of X01's
        // (SetsWon desc, LegsWon desc), since there are no legs/sets here.
        var standings = _groupStates.Values
            .OrderByDescending(g => g.ClosedCount)
            .ThenByDescending(g => g.Points)
            .SelectMany(g => g.MemberPlayerIds)
            .ToArray();

        return Task.FromResult(new GameResult(_winnerPlayerIds!, standings));
    }

    private void EnsureNotComplete()
    {
        if (_isComplete)
            throw new GameRuleViolationException("The game has already finished.");
    }

    private void Rebuild()
    {
        var distinctGroups = _players.Select(id => _groupByPlayer[id]).Distinct();
        _groupStates = distinctGroups.ToDictionary(
            g => g,
            g => new CricketGroupState(g, _players.Where(id => _groupByPlayer[id] == g).ToArray()));

        _currentPlayerIndex = 0;
        _currentVisitThrows = [];
        _allThrows = [];
        _isComplete = false;
        _winnerPlayerIds = null;

        foreach (var entry in _log)
        {
            if (entry.Kind == LogEntryKind.EndOfTurn)
            {
                AdvanceToNextPlayer();
                _currentVisitThrows = [];
                continue;
            }

            var detectedThrow = entry.Throw!;
            var throwingPlayerId = _players[_currentPlayerIndex];
            var group = _groupStates[_groupByPlayer[throwingPlayerId]];

            _currentVisitThrows.Add(detectedThrow);
            _allThrows.Add(detectedThrow);

            ApplyThrow(group, detectedThrow);

            if (IsWinningGroup(group))
            {
                _isComplete = true;
                _winnerPlayerIds = group.MemberPlayerIds;
                break;
            }

            if (_currentVisitThrows.Count == 3)
            {
                AdvanceToNextPlayer();
                _currentVisitThrows = [];
            }
        }
    }

    private void ApplyThrow(CricketGroupState group, DetectedThrow detectedThrow)
    {
        var targetIndex = CricketTargets.IndexFor(detectedThrow.Ring, detectedThrow.Segment);
        if (targetIndex < 0) return; // miss, or a non-Cricket number (1-14): counts toward the visit, no other effect

        var hitMarks = CricketTargets.MarksForRing(detectedThrow.Ring);
        if (hitMarks == 0) return;

        var current = group.Marks[targetIndex];
        var toClose = Math.Min(CricketTargets.MarksToClose - current, hitMarks);
        var overflow = hitMarks - toClose;
        group.Marks[targetIndex] = current + toClose;

        if (overflow > 0 && AnyOtherGroupStillOpen(group.GroupIndex, targetIndex))
            group.Points += overflow * CricketTargets.PointValue(targetIndex);
    }

    private bool AnyOtherGroupStillOpen(int groupIndex, int targetIndex) =>
        _groupStates.Values.Any(g => g.GroupIndex != groupIndex && g.Marks[targetIndex] < CricketTargets.MarksToClose);

    private bool IsWinningGroup(CricketGroupState group) =>
        group.HasClosedEverything
        && _groupStates.Values.Where(g => g.GroupIndex != group.GroupIndex).All(g => group.Points > g.Points);

    private void AdvanceToNextPlayer()
    {
        _currentPlayerIndex = (_currentPlayerIndex + 1) % _players.Count;
    }
}
