using Barrelo.GameSdk;

namespace Barrelo.Games.X01;

public sealed class X01GameFactory : IGameFactory
{
    public const string GameId = "x01";

    public GameDescriptor Describe() => new(
        GameId,
        "X01",
        "Classic X01 — race to zero with a double-out finish.",
        new GameSettingDefinition[]
        {
            new GameModeSetting(
                Key: "startingScore",
                DisplayName: "Starting score",
                Choices:
                [
                    new GameModeChoice("501", "501", new Dictionary<string, string> { ["startingScore"] = "501" }),
                    new GameModeChoice("301", "301", new Dictionary<string, string> { ["startingScore"] = "301" }),
                    new GameModeChoice("701", "701", new Dictionary<string, string> { ["startingScore"] = "701" }),
                ],
                DefaultValue: "501"),
            new PlayerGroupSetting(
                Key: "teams",
                DisplayName: "Teams",
                MaxGroups: 2,
                MaxPlayersPerGroup: 4),
        });

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException("X01 requires at least one player.");

        var options = X01Options.Parse(setup.Options);
        var groupByPlayer = setup.PlayerIds.ToDictionary(id => id, setup.EffectiveGroupIndex);
        return Task.FromResult<IGame>(new X01Game(setup.PlayerIds, groupByPlayer, options));
    }
}
