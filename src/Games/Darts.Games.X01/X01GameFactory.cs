using Darts.GameSdk;

namespace Darts.Games.X01;

public sealed class X01GameFactory : IGameFactory
{
    public const string GameId = "x01";

    public GameDescriptor Describe() => new(GameId, "501", "Classic 501 — race to zero with a double-out finish.");

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException("X01 requires at least one player.");

        var options = X01Options.Parse(setup.Options);
        return Task.FromResult<IGame>(new X01Game(setup.PlayerIds, options));
    }
}
