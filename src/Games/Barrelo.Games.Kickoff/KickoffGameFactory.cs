using Barrelo.GameSdk;

namespace Barrelo.Games.Kickoff;

public sealed class KickoffGameFactory : IGameFactory
{
    public const string GameId = "kickoff";

    public GameDescriptor Describe() => new(
        GameId,
        "Kickoff",
        "Soccer on the oche — every dart is a kick toward one of two goals. Exactly two teams.",
        new GameSettingDefinition[]
        {
            new PlayerGroupSetting(
                Key: "teams",
                DisplayName: "Teams",
                MaxGroups: 2,
                MaxPlayersPerGroup: 4),
        });

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException("Kickoff requires at least one player.");

        var groupByPlayer = setup.PlayerIds.ToDictionary(id => id, setup.EffectiveGroupIndex);
        var distinctGroups = groupByPlayer.Values.Distinct().Count();
        if (distinctGroups != 2)
            throw new GameRuleViolationException("Kickoff requires exactly two teams with at least one player each.");

        var options = KickoffOptions.Parse(setup.Options);
        return Task.FromResult<IGame>(new KickoffGame(setup.PlayerIds, groupByPlayer, options));
    }
}
