using Barrelo.GameSdk;

namespace Barrelo.Games.Cricket;

public sealed class CricketGameFactory : IGameFactory
{
    public const string GameId = "cricket";

    public GameDescriptor Describe() => new(
        GameId,
        "Cricket",
        "Standard Cricket — close 15 through 20 and Bull, then outscore the field.",
        new GameSettingDefinition[]
        {
            new PlayerGroupSetting(
                Key: "teams",
                DisplayName: "Teams",
                MaxGroups: 4,
                MaxPlayersPerGroup: 4),
        });

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException("Cricket requires at least one player.");

        var groupByPlayer = setup.PlayerIds.ToDictionary(id => id, setup.EffectiveGroupIndex);
        return Task.FromResult<IGame>(new CricketGame(setup.PlayerIds, groupByPlayer));
    }
}
