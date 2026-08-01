using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock.UnitTests;

internal static class AroundTheClockTestGame
{
    /// <summary>The rung a team stands on when only the bull is left.</summary>
    public const int BullStep = 20;

    /// <summary>Darts a visit holds. Reaching it no longer ends the visit — only an EndOfTurn does.</summary>
    public const int DartsPerVisit = 3;

    public static async Task<IGame> Create(
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<Guid, int>? playerGroups = null)
    {
        var setup = new GameSetup(players, new Dictionary<string, string>(), playerGroups);
        return await new AroundTheClockGameFactory().Create(setup, CancellationToken.None);
    }

    public static async Task<AroundTheClockStatePayload> Payload(this IGame game)
    {
        var state = await game.GetState();
        return (AroundTheClockStatePayload)state.Payload!;
    }

    public static AroundTheClockGroupProgress GroupFor(this AroundTheClockStatePayload payload, Guid playerId) =>
        payload.Groups.Single(g => g.PlayerIds.Contains(playerId));

    public static async Task<AroundTheClockGroupProgress> GroupOf(this IGame game, Guid playerId) =>
        (await game.Payload()).GroupFor(playerId);

    /// <summary>
    /// Hits the live target with plain singles until the throwing team reaches <paramref name="progress"/>,
    /// ending each turn once three darts are in — nothing ends a visit on dart count, so the takeout has
    /// to be simulated. Only safe on a single-group game, where the turn always comes straight back.
    /// Returns with an empty visit, so the caller always has three darts to play with.
    /// </summary>
    public static async Task ClimbSolo(this IGame game, int progress)
    {
        while (true)
        {
            var payload = await game.Payload();
            var group = payload.Groups.Single();
            var reached = group.Progress >= progress || group.TargetNumber is null;

            if (payload.CurrentVisitThrows.Count >= DartsPerVisit || (reached && payload.CurrentVisitThrows.Count > 0))
            {
                await game.ReceiveEndOfTurn(CancellationToken.None);
                continue;
            }

            if (reached) return;

            await game.ReceiveThrow(TestThrow.Of(Ring.OuterSingle, group.TargetNumber!.Value), CancellationToken.None);
        }
    }

    /// <summary>
    /// Drives a multi-team game until <paramref name="playerId"/>'s team is standing on the bull with a
    /// fresh visit in front of them: they hit their live target with singles, every other team misses, and
    /// every turn is ended explicitly. Leaves the caller free to throw only the dart the test is about.
    /// </summary>
    public static async Task ClimbToBull(this IGame game, Guid playerId)
    {
        for (var guard = 0; guard < 500; guard++)
        {
            var state = await game.GetState();
            var payload = (AroundTheClockStatePayload)state.Payload!;
            var throwing = payload.GroupFor(state.CurrentPlayerId!.Value);
            var isTargetTeam = throwing.PlayerIds.Contains(playerId);

            if (isTargetTeam && throwing.Progress >= BullStep && payload.CurrentVisitThrows.Count == 0)
                return;

            if (payload.CurrentVisitThrows.Count >= DartsPerVisit)
            {
                await game.ReceiveEndOfTurn(CancellationToken.None);
                continue;
            }

            await game.ReceiveThrow(
                isTargetTeam && throwing.TargetNumber is int target
                    ? TestThrow.Of(Ring.OuterSingle, target)
                    : TestThrow.Of(Ring.Miss),
                CancellationToken.None);
        }

        throw new InvalidOperationException("ClimbToBull never reached the bull step.");
    }
}
