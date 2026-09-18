using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock;

/// <summary>
/// Around The Clock: every team climbs the same 21-rung ladder — 1 through 20, then the bull — and the
/// first to hit the inner bull ends the match on the spot. Like X01/Cricket, undo works by replaying the
/// full append-only event log from scratch rather than reversing deltas, so undoing the dart that won the
/// match needs no special case of its own.
///
/// Two rules here differ from the plain pub version and are worth stating outright:
/// a double on your number advances you two rungs and a treble three, and that jump is *clamped* at the
/// bull step so a treble 19 leaves you on the bull rather than through it. The bull is then the one place
/// the ring is the rule: only the inner 50 (<see cref="Ring.Double"/> at segment 25) finishes, while the
/// outer 25 ring counts for nothing at all.
///
/// Turn order is a round-robin over *teams*, not over players (unlike Cricket, which walks the flat
/// roster). In a straight race to the bull, visit count is the whole game, so a three-player team taking
/// three visits a round against a two-player team's two would be close to decisive. Each team therefore
/// gets exactly one visit per round and rotates its own thrower via
/// <see cref="AroundTheClockGroupState.NextMemberIndex"/> — a round-robin per team, generalised to N
/// teams rather than just two.
/// </summary>
public sealed class AroundTheClockGame : IGame
{
    /// <summary>
    /// A visit holds at most this many darts, but reaching it does not end the visit — only an explicit
    /// EndOfTurn does. Turn order follows the physical takeout, which is what a detection source reports,
    /// so the throwing team stays on the oche until the darts actually leave the board.
    /// </summary>
    private const int DartsPerVisit = 3;

    private const int BullSegment = 25;

    private enum LogEntryKind { Throw, EndOfTurn }

    private sealed record LogEntry(LogEntryKind Kind, DetectedThrow? Throw);

    private readonly IReadOnlyList<Guid> _players;
    private readonly IReadOnlyDictionary<Guid, int> _groupByPlayer;
    private readonly List<LogEntry> _log = [];

    // Derived state, fully recomputed by Rebuild() after every log mutation.
    private int[] _groupOrder = [];
    private Dictionary<int, AroundTheClockGroupState> _groupStates = [];
    private int _currentGroupPos;
    private int _round = 1;
    private List<DetectedThrow> _currentVisitThrows = [];
    private List<int> _currentVisitSteps = [];
    private List<DetectedThrow> _allThrows = [];
    private bool _isComplete;
    private IReadOnlyList<Guid>? _winnerPlayerIds;

    internal AroundTheClockGame(IReadOnlyList<Guid> players, IReadOnlyDictionary<Guid, int> groupByPlayer)
    {
        _players = players;
        _groupByPlayer = groupByPlayer;
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
        var payload = new AroundTheClockStatePayload(
            _groupStates.Values
                .OrderBy(g => g.GroupIndex)
                .Select(g => new AroundTheClockGroupProgress(
                    g.GroupIndex,
                    g.MemberPlayerIds,
                    g.Progress,
                    AroundTheClockTargets.TargetNumberFor(g.Progress),
                    g.Progress == AroundTheClockTargets.BullStep,
                    g.IsFinished))
                .ToArray(),
            _round,
            AroundTheClockTargets.TotalSteps,
            _currentVisitThrows.ToArray(),
            _currentVisitSteps.ToArray());

        var snapshot = new GameStateSnapshot(
            MatchId: Guid.Empty, // the plugin doesn't know its own MatchId; the host stamps it in
            GameId: AroundTheClockGameFactory.GameId,
            Status: _isComplete ? GameStatus.Complete : GameStatus.InProgress,
            CurrentPlayerId: _isComplete ? null : CurrentThrowerId,
            // Around The Clock has rounds, not legs or sets. A round isn't a leg, so rather than
            // mislabel one as the other in the game-agnostic envelope it stays 1/1 and the round
            // travels in the payload, where the board renders it.
            LegNumber: 1,
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

        // The winning team is the only one at TotalSteps, so ordering by progress puts them first on
        // its own — everyone behind them places by how far up the clock they got, which is what the
        // session leaderboard chunks into 1st/2nd/3rd.
        var standings = _groupStates.Values
            .OrderByDescending(g => g.Progress)
            .ThenBy(g => g.GroupIndex)
            .SelectMany(g => g.MemberPlayerIds)
            .ToArray();

        return Task.FromResult(new GameResult(_winnerPlayerIds!, standings));
    }

    private AroundTheClockGroupState CurrentGroup => _groupStates[_groupOrder[_currentGroupPos]];

    private Guid CurrentThrowerId => CurrentGroup.MemberPlayerIds[CurrentGroup.NextMemberIndex];

    private void EnsureNotComplete()
    {
        if (_isComplete)
            throw new GameRuleViolationException("The game has already finished.");
    }

    /// <summary>
    /// Nothing ends a visit on dart count any more, so a fourth dart would otherwise keep climbing the
    /// clock for a team whose three darts are already in the board — and stay invisible, since the visit
    /// is only ever displayed three darts wide. Refusing it surfaces the missing takeout instead.
    /// </summary>
    private void EnsureVisitHasRoom()
    {
        if (_currentVisitThrows.Count >= DartsPerVisit)
            throw new GameRuleViolationException(
                "This visit already has three darts — end the turn before throwing again.");
    }

    private void Rebuild()
    {
        _groupOrder = _players.Select(id => _groupByPlayer[id]).Distinct().OrderBy(g => g).ToArray();
        _groupStates = _groupOrder.ToDictionary(
            g => g,
            g => new AroundTheClockGroupState(g, _players.Where(id => _groupByPlayer[id] == g).ToArray()));

        _currentGroupPos = 0;
        _round = 1;
        _currentVisitThrows = [];
        _currentVisitSteps = [];
        _allThrows = [];
        _isComplete = false;
        _winnerPlayerIds = null;

        // EndOfTurn is the only thing that hands the oche over: no rule here cuts a visit short, and dart
        // count deliberately doesn't either. A detection source fires EndOfTurn once the board is
        // cleared, so the team keeps the throw until their darts are physically out of the board.
        foreach (var entry in _log)
        {
            if (entry.Kind == LogEntryKind.EndOfTurn)
            {
                EndVisit();
                continue;
            }

            var detectedThrow = entry.Throw!;
            var group = CurrentGroup;

            _currentVisitThrows.Add(detectedThrow);
            _allThrows.Add(detectedThrow);
            _currentVisitSteps.Add(ApplyThrow(group, detectedThrow));

            if (group.IsFinished)
            {
                _isComplete = true;
                _winnerPlayerIds = group.MemberPlayerIds;
                break;
            }
        }
    }

    /// <summary>Applies one dart and returns how many rungs it earned, for the board's per-dart label.</summary>
    private static int ApplyThrow(AroundTheClockGroupState group, DetectedThrow detectedThrow)
    {
        if (group.Progress >= AroundTheClockTargets.BullStep)
        {
            // Inner bull only. Ring.Single at segment 25 is the outer 25 ring and does nothing here —
            // "finish on the bull" means the 50.
            if (detectedThrow.Ring != Ring.Double || detectedThrow.Segment != BullSegment)
                return 0;

            group.Progress = AroundTheClockTargets.TotalSteps;
            return 1;
        }

        // Number targets are 1-20 and the bull is segment 25, so the two can never collide and no
        // explicit DartScoring.IsBull guard is needed on this branch.
        if (detectedThrow.Segment != AroundTheClockTargets.TargetNumberFor(group.Progress))
            return 0;

        var steps = AroundTheClockTargets.StepsFor(detectedThrow.Ring);
        if (steps == 0)
            return 0;

        // Clamped at the bull step rather than carried through it: a treble 19 leaves you *on* the
        // bull, never past it, so every match still ends on a bullseye.
        var advanced = Math.Min(group.Progress + steps, AroundTheClockTargets.BullStep);
        var gained = advanced - group.Progress;
        group.Progress = advanced;
        return gained;
    }

    private void EndVisit()
    {
        var outgoing = CurrentGroup;
        outgoing.NextMemberIndex = (outgoing.NextMemberIndex + 1) % outgoing.MemberPlayerIds.Count;

        _currentGroupPos = (_currentGroupPos + 1) % _groupOrder.Length;
        if (_currentGroupPos == 0)
            _round++;

        _currentVisitThrows = [];
        _currentVisitSteps = [];
    }
}
