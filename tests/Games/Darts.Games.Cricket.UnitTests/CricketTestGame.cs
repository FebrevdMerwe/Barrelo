using Darts.GameSdk;

namespace Darts.Games.Cricket.UnitTests;

internal static class CricketTestGame
{
    public static async Task<IGame> Create(
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<Guid, int>? playerGroups = null)
    {
        var setup = new GameSetup(players, new Dictionary<string, string>(), playerGroups);
        return await new CricketGameFactory().Create(setup, CancellationToken.None);
    }

    public static async Task<CricketStatePayload> Payload(this IGame game)
    {
        var state = await game.GetState();
        return (CricketStatePayload)state.Payload!;
    }

    public static CricketGroupScore GroupFor(this CricketStatePayload payload, Guid playerId) =>
        payload.Groups.Single(g => g.PlayerIds.Contains(playerId));
}
