using Barrelo.GameSdk;
using Barrelo.Infrastructure.External.GamePlugins;
using FluentAssertions;

namespace Barrelo.Infrastructure.IntegrationTests.GamePlugins;

/// <summary>
/// ClientOwnedGame holds the visit log every client replays, so its grouping and undo behaviour is the
/// one piece of a client-owned game the host is actually responsible for getting right.
/// </summary>
public sealed class ClientOwnedGameTests
{
    private static readonly Guid PlayerOne = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PlayerTwo = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ClientOwnedGame CreateGame() => new(
        "testgame",
        seed: 42,
        new GameSetup(
            [PlayerOne, PlayerTwo],
            new Dictionary<string, string> { ["mode"] = "quick" },
            new Dictionary<Guid, int> { [PlayerOne] = 0, [PlayerTwo] = 1 }));

    private static DetectedThrow Throw(int segment, Ring ring) => new(
        ThrowId: Guid.NewGuid(),
        Segment: segment,
        Ring: ring,
        Score: DartScoring.Score(ring, segment),
        RawNotation: DartScoring.Notation(ring, segment),
        Position: BoardGeometry.CenterOf(segment, ring),
        Confidence: null,
        BoardId: "test-board",
        CameraIndex: null,
        DetectedAtUtc: DateTimeOffset.UnixEpoch,
        Source: DetectionSourceType.Manual);

    private static async Task<ClientGamePayload> PayloadOf(ClientOwnedGame game) =>
        (ClientGamePayload)(await game.GetState()).Payload!;

    [Fact]
    public async Task A_new_game_has_no_visits_at_all()
    {
        var game = CreateGame();

        var payload = await PayloadOf(game);

        // Visits are created lazily, on the first dart — an "open but empty" visit never exists.
        payload.Visits.Should().BeEmpty();
        payload.Seed.Should().Be(42);
    }

    [Fact]
    public async Task Every_push_carries_the_roster_so_a_client_arriving_late_can_rebuild_from_one_message()
    {
        var game = CreateGame();

        // A TV switched on mid-match gets one snapshot and nothing else — there is no setup handshake
        // for it to have missed, so who's playing has to be in the payload every time.
        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);

        var payload = await PayloadOf(game);
        payload.PlayerIds.Should().Equal(PlayerOne, PlayerTwo);
        payload.PlayerGroups.Should().Contain(new KeyValuePair<Guid, int>(PlayerTwo, 1));
        payload.Options.Should().Contain(new KeyValuePair<string, string>("mode", "quick"));
    }

    [Fact]
    public async Task Throws_accumulate_into_one_open_visit_until_the_turn_ends()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveThrow(Throw(20, Ring.OuterSingle), CancellationToken.None);

        var payload = await PayloadOf(game);
        payload.Visits.Should().ContainSingle();
        payload.Visits[0].Throws.Should().HaveCount(2);
        payload.Visits[0].Ended.Should().BeFalse();
    }

    [Fact]
    public async Task Ending_a_turn_closes_the_open_visit_and_the_next_throw_opens_a_new_one()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(Throw(19, Ring.Double), CancellationToken.None);

        var payload = await PayloadOf(game);
        payload.Visits.Should().HaveCount(2);
        payload.Visits[0].Ended.Should().BeTrue();
        payload.Visits[1].Ended.Should().BeFalse();
        payload.Visits[1].Throws.Should().ContainSingle();
    }

    [Fact]
    public async Task Ending_a_turn_twice_in_a_row_does_not_create_an_empty_visit()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);

        var payload = await PayloadOf(game);
        payload.Visits.Should().ContainSingle();
    }

    [Fact]
    public async Task Undo_after_a_throw_removes_that_throw()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveThrow(Throw(5, Ring.Miss), CancellationToken.None);
        await game.UndoLastThrow(CancellationToken.None);

        var payload = await PayloadOf(game);
        payload.Visits[0].Throws.Should().ContainSingle();
        payload.Visits[0].Throws[0].RawNotation.Should().Be("T20");
    }

    [Fact]
    public async Task Undo_after_ending_a_turn_reopens_that_visit_rather_than_dropping_a_dart()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.UndoLastThrow(CancellationToken.None);

        // Ending the turn was the most recent thing to happen, so that's what undo takes back.
        var payload = await PayloadOf(game);
        payload.Visits.Should().ContainSingle();
        payload.Visits[0].Ended.Should().BeFalse();
        payload.Visits[0].Throws.Should().ContainSingle();
    }

    [Fact]
    public async Task Undoing_the_only_throw_of_a_visit_removes_the_visit_and_reopens_the_previous_one()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(Throw(19, Ring.Double), CancellationToken.None);

        await game.UndoLastThrow(CancellationToken.None); // drops the second visit entirely
        await game.UndoLastThrow(CancellationToken.None); // then un-ends the first

        var payload = await PayloadOf(game);
        payload.Visits.Should().ContainSingle();
        payload.Visits[0].Ended.Should().BeFalse();
        payload.Visits[0].Throws.Should().ContainSingle();
    }

    [Fact]
    public async Task Undo_with_nothing_thrown_is_a_rule_violation_rather_than_a_crash()
    {
        var game = CreateGame();

        var undo = () => game.UndoLastThrow(CancellationToken.None);

        await undo.Should().ThrowAsync<GameRuleViolationException>();
    }

    [Fact]
    public async Task RecentThrows_flattens_the_visits_so_the_shell_ledger_works_without_a_payload()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);
        await game.ReceiveEndOfTurn(CancellationToken.None);
        await game.ReceiveThrow(Throw(19, Ring.Double), CancellationToken.None);

        var state = await game.GetState();
        state.RecentThrows.Select(t => t.RawNotation).Should().Equal("T20", "D19");
    }

    [Fact]
    public async Task The_host_reports_no_current_player_because_turn_order_is_a_rule_it_does_not_know()
    {
        var game = CreateGame();

        await game.ReceiveThrow(Throw(20, Ring.Triple), CancellationToken.None);

        var state = await game.GetState();
        state.CurrentPlayerId.Should().BeNull();
        state.IsComplete.Should().BeFalse();
        state.Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public async Task Reporting_a_result_completes_the_game_and_is_returned_verbatim()
    {
        var game = CreateGame();
        var winner = Guid.NewGuid();
        var loser = Guid.NewGuid();

        await game.ReportResult(new GameResult([winner], [winner, loser]), CancellationToken.None);

        game.IsComplete.Should().BeTrue();
        var state = await game.GetState();
        state.Status.Should().Be(GameStatus.Complete);
        state.WinnerPlayerIds.Should().Equal(winner);
        (await game.GetResult()).FinalStandings.Should().Equal(winner, loser);
    }
}
