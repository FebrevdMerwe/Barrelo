using System.Net.Http.Json;
using System.Text.Json;
using Darts.Api.Contracts;
using Darts.GameSdk;
using FluentAssertions;

namespace Darts.Api.IntegrationTests;

/// <summary>
/// TASKS.md's canonical Phase-1 "done when" gate: a full manual 501 leg driven entirely over real HTTP —
/// normal throws, a Miss, an early manual-end-turn, and undo of a busting dart plus undo across a leg
/// boundary — with no streaming detection source registered or running at all.
/// </summary>
public class ManualFullLegEndToEndTests(DartsApiFactory factory) : IClassFixture<DartsApiFactory>
{
    [Fact]
    public async Task Full_manual_501_leg_over_http_reaches_a_winner()
    {
        var client = factory.CreateClient();
        var playerIds = await factory.SeedPlayers("P1", "P2");
        var p1 = playerIds[0];
        var p2 = playerIds[1];

        var playerGroups = playerIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        var startResponse = await client.PostAsJsonAsync(
            "/api/matches",
            new StartMatchRequest(
                "x01",
                playerIds,
                new Dictionary<string, string> { ["startingScore"] = "41", ["legsToWin"] = "2", ["setsToWin"] = "1" },
                playerGroups),
            JsonTestOptions.Options);
        startResponse.EnsureSuccessStatusCode();

        // P1 visit 1: a Miss, then an early end-turn after only 2 darts (never reaching the 3rd).
        await Throw(0, Ring.Miss);
        await Throw(1, Ring.Inner);
        await EndTurn(); // P1 remaining: 41 -> 40

        // P2 visit 1: three misses.
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);

        // P1 visit 2: busts (40 - 60 < 0).
        var busted = await Throw(20, Ring.Triple);
        busted.CurrentPlayerId.Should().Be(p2); // turn passed to P2 on the bust

        var afterUndoBust = await Undo(); // undo the busting dart
        afterUndoBust.CurrentPlayerId.Should().Be(p1); // turn ownership reverts
        GroupFor(afterUndoBust, p1).RemainingScore.Should().Be(40);

        // Finish leg 1.
        var afterCheckout = await Throw(20, Ring.Double); // 40 -> 0, valid double-out
        afterCheckout.LegNumber.Should().Be(2);
        afterCheckout.CurrentPlayerId.Should().Be(p2); // leg 2 starts with the other player

        // Undo across the leg boundary: reverts the checkout dart itself, landing back in leg 1.
        var afterLegBoundaryUndo = await Undo();
        afterLegBoundaryUndo.LegNumber.Should().Be(1);
        afterLegBoundaryUndo.CurrentPlayerId.Should().Be(p1);
        GroupFor(afterLegBoundaryUndo, p1).LegsWon.Should().Be(0);

        // Re-finish leg 1 for real.
        var legOneWon = await Throw(20, Ring.Double);
        legOneWon.LegNumber.Should().Be(2);
        legOneWon.CurrentPlayerId.Should().Be(p2);

        // P2 visit (leg 2): three misses.
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);
        await Throw(0, Ring.Miss);

        // P1 visit (leg 2), remaining 41: finish with a double checkout to win leg 2 and the match.
        await Throw(13, Ring.Triple); // 41 -> 2
        var final = await Throw(1, Ring.Double); // 2 -> 0

        final.IsComplete.Should().BeTrue();
        final.Status.Should().Be(GameStatus.Complete);
        final.WinnerPlayerIds.Should().BeEquivalentTo([p1]);
        final.CurrentPlayerId.Should().BeNull();

        return;

        async Task<GameStateSnapshot> Throw(int segment, Ring ring)
        {
            var response = await client.PostAsJsonAsync("/api/detection/manual-throw", new ManualThrowRequest(segment, ring), JsonTestOptions.Options);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GameStateSnapshot>(JsonTestOptions.Options))!;
        }

        async Task<GameStateSnapshot> EndTurn()
        {
            var response = await client.PostAsync("/api/detection/manual-end-turn", null);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GameStateSnapshot>(JsonTestOptions.Options))!;
        }

        async Task<GameStateSnapshot> Undo()
        {
            var response = await client.PostAsync("/api/detection/undo", null);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<GameStateSnapshot>(JsonTestOptions.Options))!;
        }

        static X01GroupScoreDto GroupFor(GameStateSnapshot snapshot, Guid playerId) =>
            ((JsonElement)snapshot.Payload!)
            .GetProperty("groups")
            .Deserialize<List<X01GroupScoreDto>>(JsonTestOptions.Options)!
            .Single(g => g.PlayerIds.Contains(playerId));
    }

    private sealed record X01GroupScoreDto(int GroupIndex, IReadOnlyList<Guid> PlayerIds, int RemainingScore, int LegsWon, int SetsWon);
}
