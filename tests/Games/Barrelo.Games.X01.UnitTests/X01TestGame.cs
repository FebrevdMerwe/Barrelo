using Barrelo.GameSdk;

namespace Barrelo.Games.X01.UnitTests;

internal static class X01TestGame
{
    public static async Task<IGame> Create(
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<string, string>? options = null,
        IReadOnlyDictionary<Guid, int>? playerGroups = null)
    {
        var setup = new GameSetup(players, options ?? new Dictionary<string, string>(), playerGroups);
        return await new X01GameFactory().Create(setup, CancellationToken.None);
    }

    public static async Task<X01StatePayload> Payload(this IGame game)
    {
        var state = await game.GetState();
        return (X01StatePayload)state.Payload!;
    }

    public static X01GroupScore GroupFor(this X01StatePayload payload, Guid playerId) =>
        payload.Groups.Single(g => g.PlayerIds.Contains(playerId));
}
