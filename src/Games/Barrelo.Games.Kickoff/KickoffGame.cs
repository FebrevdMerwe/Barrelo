using Barrelo.GameSdk;

namespace Barrelo.Games.Kickoff;

/// <summary>
/// Reference third IGame implementation, ported from the mockup/plugins/kickoff prototype: one shared
/// pitch, one shared ball. Every dart is a kick — segment sets the compass direction (reusing the
/// dartboard's own angle order via <see cref="BoardGeometry.AngleDegreesForSegment"/>), ring sets how
/// hard. Sending the ball off the pitch ends the visit on the spot with the other side throwing in from
/// where it left the field; scoring a goal resets the ball to center with the conceding side kicking off.
/// Undo works the same way as Cricket/X01: replay the full append-only event log from scratch.
///
/// Unlike Cricket/X01, turn order is NOT a flat round-robin over every player: exactly two sides exist,
/// and possession must alternate strictly between them for the ball-and-two-goals mechanic to make sense
/// regardless of roster size. Each side keeps its own "who's up next" pointer, advanced every time that
/// side's visit ends (see <see cref="KickoffGroupState.NextMemberIndex"/>), while <see cref="_currentSide"/>
/// tracks which of the two sides currently holds the ball. Possession after a goal goes to whichever side
/// is <c>1 - goalOwner</c> (the side that conceded) rather than simply "the other side from whoever kicked" —
/// these are usually the same side, except on an own goal, where the kicking side concedes and therefore
/// keeps the ball for the restart.
/// </summary>
public sealed class KickoffGame : IGame
{
    // Goals are scored east/west (X axis) so the pitch's compass matches its landscape, right-to-left
    // presentation: kicking east (e.g. segment 6) drives the ball toward group 0's goal on the right,
    // west drives it toward group 1's on the left. North/south (Y) is the touchline bound instead.
    private const double GoalMouthMin = 0.35;
    private const double GoalMouthMax = 0.65;
    private const int TrailMax = 8;

    private enum LogEntryKind { Throw, EndOfTurn }

    private sealed record LogEntry(LogEntryKind Kind, DetectedThrow? Throw);

    private readonly IReadOnlyList<Guid> _players;
    private readonly IReadOnlyDictionary<Guid, int> _groupByPlayer;
    private readonly KickoffOptions _options;
    private readonly List<LogEntry> _log = [];

    // Derived state, fully recomputed by Rebuild() after every log mutation.
    private int[] _sideGroupIndex = new int[2];
    private Dictionary<int, KickoffGroupState> _groupStates = [];
    private int _currentSide;
    private BallPosition _ball = new(0.5, 0.5);
    private List<BallPosition> _trail = [];
    private KickoffEvent? _lastEvent;
    private int _legNumber = 1;
    private List<DetectedThrow> _currentVisitThrows = [];
    private List<DetectedThrow> _currentLegThrows = [];
    private bool _isComplete;
    private IReadOnlyList<Guid>? _winnerPlayerIds;

    internal KickoffGame(IReadOnlyList<Guid> players, IReadOnlyDictionary<Guid, int> groupByPlayer, KickoffOptions options)
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
        var payload = new KickoffStatePayload(
            _groupStates.Values
                .OrderBy(g => g.GroupIndex)
                .Select(g => new KickoffGroupScore(g.GroupIndex, g.MemberPlayerIds, g.Goals, g.LegsWon))
                .ToArray(),
            _ball,
            _trail.ToArray(),
            _lastEvent,
            _currentVisitThrows.ToArray());

        var snapshot = new GameStateSnapshot(
            MatchId: Guid.Empty, // the plugin doesn't know its own MatchId; the host stamps it in
            GameId: KickoffGameFactory.GameId,
            Status: _isComplete ? GameStatus.Complete : GameStatus.InProgress,
            CurrentPlayerId: _isComplete ? null : CurrentSideState.MemberPlayerIds[CurrentSideState.NextMemberIndex],
            LegNumber: _legNumber,
            SetNumber: 1, // Kickoff has legs but no separate best-of-sets layer, matching the mockup
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
            .OrderByDescending(g => g.LegsWon)
            .ThenByDescending(g => g.Goals)
            .SelectMany(g => g.MemberPlayerIds)
            .ToArray();

        return Task.FromResult(new GameResult(_winnerPlayerIds!, standings));
    }

    private KickoffGroupState CurrentSideState => _groupStates[_sideGroupIndex[_currentSide]];

    private void EnsureNotComplete()
    {
        if (_isComplete)
            throw new GameRuleViolationException("The game has already finished.");
    }

    private void Rebuild()
    {
        var distinctGroups = _players.Select(id => _groupByPlayer[id]).Distinct().OrderBy(g => g).ToArray();
        _sideGroupIndex = distinctGroups;
        _groupStates = distinctGroups.ToDictionary(
            g => g,
            g => new KickoffGroupState(g, _players.Where(id => _groupByPlayer[id] == g).ToArray()));

        _currentSide = 0;
        _ball = new BallPosition(0.5, 0.5);
        _trail = [new BallPosition(0.5, 0.5)];
        _lastEvent = null;
        _legNumber = 1;
        _currentVisitThrows = [];
        _currentLegThrows = [];
        _isComplete = false;
        _winnerPlayerIds = null;

        foreach (var entry in _log)
        {
            if (entry.Kind == LogEntryKind.EndOfTurn)
            {
                _lastEvent = null;
                EndVisit(nextSide: 1 - _currentSide, resetBall: false);
                continue;
            }

            ApplyKick(entry.Throw!);
            if (_isComplete) break;
        }
    }

    private void ApplyKick(DetectedThrow detectedThrow)
    {
        var throwingSide = _currentSide;
        _currentVisitThrows.Add(detectedThrow);
        _currentLegThrows.Add(detectedThrow);
        _lastEvent = null;

        var magnitude = Magnitude(detectedThrow.Ring);
        var angleDeg = detectedThrow.Ring is Ring.Miss or Ring.InnerBull or Ring.OuterBull
            ? 0.0
            : BoardGeometry.AngleDegreesForSegment(detectedThrow.Segment);
        var rad = angleDeg * Math.PI / 180.0;
        var newX = _ball.X + Math.Sin(rad) * magnitude;
        var newY = _ball.Y - Math.Cos(rad) * magnitude;

        string outcome;
        int? goalOwner = null;
        if (newX >= 1)
        {
            if (InGoalMouth(newY)) { outcome = "goal"; goalOwner = 0; }
            else outcome = "out";
        }
        else if (newX <= 0)
        {
            if (InGoalMouth(newY)) { outcome = "goal"; goalOwner = 1; }
            else outcome = "out";
        }
        else if (newY < 0 || newY > 1)
        {
            outcome = "out";
        }
        else
        {
            outcome = "move";
        }

        if (outcome == "out")
        {
            _ball = new BallPosition(Clamp01(newX), Clamp01(newY));
            PushTrail();
            _lastEvent = new KickoffEvent("OUT! — their throw-in", "bad");
            EndVisit(nextSide: 1 - throwingSide, resetBall: false);
            return;
        }

        if (outcome == "goal")
        {
            var owner = goalOwner!.Value;
            var ownGoal = owner != throwingSide;
            var scoringGroup = _groupStates[_sideGroupIndex[owner]];
            scoringGroup.Goals++;
            _lastEvent = new KickoffEvent(ownGoal ? "OWN GOAL!" : "GOAL!", ownGoal ? "bad" : "good");

            if (scoringGroup.Goals >= _options.GoalsToWinLeg)
            {
                scoringGroup.LegsWon++;
                _legNumber++;
                _currentLegThrows = [];

                if (scoringGroup.LegsWon >= _options.LegsToWinMatch)
                {
                    _isComplete = true;
                    _winnerPlayerIds = scoringGroup.MemberPlayerIds;
                    return;
                }

                foreach (var g in _groupStates.Values)
                    g.Goals = 0;
            }

            // The side that conceded (1 - owner) kicks off — usually the other side from whoever just
            // kicked, except on an own goal, where the kicking side concedes and keeps the ball.
            EndVisit(nextSide: 1 - owner, resetBall: true);
            return;
        }

        _ball = new BallPosition(newX, newY);
        PushTrail();

        if (_currentVisitThrows.Count == 3)
            EndVisit(nextSide: 1 - throwingSide, resetBall: false);
    }

    private static bool InGoalMouth(double y) => y is >= GoalMouthMin and <= GoalMouthMax;

    private void EndVisit(int nextSide, bool resetBall)
    {
        var outgoingSide = _groupStates[_sideGroupIndex[_currentSide]];
        outgoingSide.NextMemberIndex = (outgoingSide.NextMemberIndex + 1) % outgoingSide.MemberPlayerIds.Count;

        _currentSide = nextSide;
        _currentVisitThrows = [];

        if (resetBall)
        {
            _ball = new BallPosition(0.5, 0.5);
            _trail = [new BallPosition(0.5, 0.5)];
        }
    }

    private void PushTrail()
    {
        _trail.Add(_ball);
        if (_trail.Count > TrailMax)
            _trail.RemoveAt(0);
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

    private static double Magnitude(Ring ring) => ring switch
    {
        Ring.InnerBull => 0.08,
        Ring.OuterBull => 0.08,
        Ring.Inner => 0.16,
        Ring.Outer => 0.26,
        Ring.Triple => 0.38,
        Ring.Double => 0.5,
        _ => 0.0,
    };
}
