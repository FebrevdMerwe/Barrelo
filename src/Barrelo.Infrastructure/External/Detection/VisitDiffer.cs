using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.Detection;

/// <summary>The darts in a reported visit that haven't been emitted yet, plus whether that report began a
/// new visit — see <see cref="VisitDiffer"/> for why the second half matters.</summary>
public sealed record VisitDiff(IReadOnlyList<DetectedThrow> NewThrows, bool StartedNewVisit);

/// <summary>
/// Turns a detector's absolute "here is the whole current visit" reports into the per-dart events the
/// rest of the platform consumes, so IGame keeps its one simple contract (ReceiveThrow, one dart at a
/// time) no matter which shape a detector natively speaks.
///
/// The reason this exists rather than the adapter diffing for itself: an adapter that tracks "how many
/// darts have I already emitted" is only correct while its connection is. AutoDarts' adapter previously
/// held that count and cleared it on the takeout event alone, so a WebSocket reconnect that straddled a
/// takeout left the count stale at 3 — and every dart of the following visit, arriving at indices 0..2,
/// was silently swallowed.
///
/// So the rule here is that the last-seen visit is only ever trusted as a *prefix*. If the incoming visit
/// doesn't extend it — it shrank, or a dart at a shared index differs — that's a new visit (or a
/// resynchronised stream), and the differ resets and replays the whole thing rather than trying to
/// reconcile. Being wrong then costs a duplicate dart, which a human can undo; the old behaviour lost
/// darts silently, which a human cannot even notice.
///
/// A visit that *shrank* is reported separately, as <see cref="VisitDiff.StartedNewVisit"/>. Fewer darts
/// on the board than last time can only mean the board was cleared, so if no takeout event arrived the
/// stream must have been down when it happened. The caller needs that distinction: without closing the
/// previous turn the game still has the previous player at the oche holding a full visit, and refuses
/// every dart of the new one as a fourth dart — the match simply stops. A mismatch at equal-or-greater
/// length is deliberately *not* reported as a new visit, because that shape is a detector revising a call
/// it already made, not a takeout.
/// </summary>
public sealed class VisitDiffer
{
    private List<DetectedThrow> _lastSeen = [];

    public VisitDiff Diff(IReadOnlyList<DetectedThrow> visit)
    {
        var startedNewVisit = _lastSeen.Count > 0 && visit.Count < _lastSeen.Count;

        if (!ExtendsLastSeen(visit))
            _lastSeen = [];

        var newThrows = visit.Skip(_lastSeen.Count).ToList();
        _lastSeen = [.. visit];
        return new VisitDiff(newThrows, startedNewVisit);
    }

    /// <summary>Called at a turn boundary: the next visit starts from nothing.</summary>
    public void Reset() => _lastSeen = [];

    private bool ExtendsLastSeen(IReadOnlyList<DetectedThrow> visit)
    {
        if (visit.Count < _lastSeen.Count)
            return false;

        // Compared on the detector's own segment/ring rather than ThrowId, because an adapter mints a
        // fresh ThrowId per message — the same physical dart resent in a growing array is a different
        // ThrowId every time.
        for (var i = 0; i < _lastSeen.Count; i++)
        {
            if (_lastSeen[i].Segment != visit[i].Segment || _lastSeen[i].Ring != visit[i].Ring)
                return false;
        }

        return true;
    }
}
