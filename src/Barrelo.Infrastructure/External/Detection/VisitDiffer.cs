using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.Detection;

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
/// </summary>
public sealed class VisitDiffer
{
    private List<DetectedThrow> _lastSeen = [];

    /// <summary>The darts in <paramref name="visit"/> that haven't been emitted yet.</summary>
    public IReadOnlyList<DetectedThrow> Diff(IReadOnlyList<DetectedThrow> visit)
    {
        if (!ExtendsLastSeen(visit))
            _lastSeen = [];

        var newThrows = visit.Skip(_lastSeen.Count).ToList();
        _lastSeen = [.. visit];
        return newThrows;
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
