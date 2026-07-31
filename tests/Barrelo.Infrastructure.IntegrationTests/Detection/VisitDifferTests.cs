using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.Detection;
using FluentAssertions;

namespace Barrelo.Infrastructure.IntegrationTests.Detection;

/// <summary>
/// A detector like AutoDarts resends the whole current visit on every dart, so something has to work out
/// which darts are new. This used to live in the adapter, keyed off a count that only reset on the takeout
/// event — which meant a reconnect straddling a takeout left it stale and swallowed a whole visit. These
/// tests pin the replacement's self-correcting behaviour, that case included.
/// </summary>
public sealed class VisitDifferTests
{
    private static DetectedThrow Throw(int segment, Ring ring) => new(
        ThrowId: Guid.NewGuid(),
        Segment: segment,
        Ring: ring,
        Score: DartScoring.Score(ring, segment),
        RawNotation: DartScoring.Notation(ring, segment),
        Position: BoardGeometry.CenterOf(segment, ring),
        Confidence: null,
        BoardId: "autodarts",
        CameraIndex: null,
        DetectedAtUtc: DateTimeOffset.UnixEpoch,
        Source: DetectionSourceType.AutoDarts);

    private static readonly DetectedThrow T20 = Throw(20, Ring.Triple);
    private static readonly DetectedThrow S5 = Throw(5, Ring.OuterSingle);
    private static readonly DetectedThrow D19 = Throw(19, Ring.Double);

    [Fact]
    public void A_visit_growing_one_dart_at_a_time_yields_exactly_the_new_dart_each_time()
    {
        var differ = new VisitDiffer();

        differ.Diff([T20]).Should().Equal(T20);
        differ.Diff([T20, S5]).Should().Equal(S5);
        differ.Diff([T20, S5, D19]).Should().Equal(D19);
    }

    [Fact]
    public void A_repeated_identical_message_yields_nothing()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5]);

        differ.Diff([T20, S5]).Should().BeEmpty();
    }

    [Fact]
    public void Reconnecting_mid_visit_and_being_resent_the_same_darts_yields_nothing()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5]);

        // The socket dropped and the board manager resent current state on reconnect. Same darts, same
        // order — nothing new to record.
        differ.Diff([T20, S5]).Should().BeEmpty();
        differ.Diff([T20, S5, D19]).Should().Equal(D19);
    }

    [Fact]
    public void A_reconnect_that_straddles_a_takeout_still_reports_the_next_visit()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5, D19]);

        // The takeout event never arrived — it fell in the reconnect gap — so Reset() was never called and
        // the differ still believes three darts are on the board. The next visit's first dart arrives at
        // index 0. Under the old count-based diffing this yielded nothing, and every dart of this visit
        // was lost until a takeout happened to get through.
        differ.Diff([S5]).Should().Equal(S5);
        differ.Diff([S5, T20]).Should().Equal(T20);
    }

    [Fact]
    public void A_visit_that_shrinks_after_a_correction_is_replayed_from_scratch()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5, D19]);

        // Fewer darts than before can't be an extension of what we saw, so the differ stops trusting its
        // history and treats the whole array as new.
        differ.Diff([T20, S5]).Should().Equal(T20, S5);
    }

    [Fact]
    public void A_dart_changing_at_an_already_seen_index_replays_the_whole_visit()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5]);

        // The detector revised its call for the second dart. The prefix no longer matches, so rather than
        // silently keeping the stale dart, everything is re-reported — a duplicate a human can undo beats
        // a wrong score nobody notices.
        differ.Diff([T20, D19]).Should().Equal(T20, D19);
    }

    [Fact]
    public void Reset_at_the_turn_boundary_makes_the_next_visit_start_from_nothing()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5, D19]);

        differ.Reset();

        differ.Diff([T20]).Should().Equal(T20);
    }

    [Fact]
    public void An_empty_visit_report_yields_nothing_and_clears_the_history()
    {
        var differ = new VisitDiffer();
        differ.Diff([T20, S5]);

        differ.Diff([]).Should().BeEmpty();
        differ.Diff([D19]).Should().Equal(D19);
    }
}
