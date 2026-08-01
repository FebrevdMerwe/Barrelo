using Barrelo.GameSdk;

namespace Barrelo.Games.AroundTheClock;

public sealed class AroundTheClockGameFactory : IGameFactory
{
    public const string GameId = "around-the-clock";

    public GameDescriptor Describe() => new(
        GameId,
        "Around The Clock",
        "Race up the board from 1 to 20 and finish on the inner bull. A double on your number jumps you "
            + "two rungs, a treble three — but the bull itself has to be hit for real.",
        new GameSettingDefinition[]
        {
            new PlayerGroupSetting(
                Key: "teams",
                DisplayName: "Teams",
                MaxGroups: 6,
                MaxPlayersPerGroup: 4),
        });

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException("Around The Clock requires at least one player.");

        var groupByPlayer = setup.PlayerIds.ToDictionary(id => id, setup.EffectiveGroupIndex);
        return Task.FromResult<IGame>(new AroundTheClockGame(setup.PlayerIds, groupByPlayer));
    }
}
