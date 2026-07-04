namespace Barrelo.GameSdk;

public static class GameSetupExtensions
{
    /// <summary>Resolves a player's effective group index: their explicit assignment if present,
    /// otherwise their own position in PlayerIds (an implicit group of one). Centralizes the
    /// "ungrouped play is unaffected" fallback so every current/future plugin gets it for free.</summary>
    public static int EffectiveGroupIndex(this GameSetup setup, Guid playerId)
    {
        if (setup.PlayerGroups is not null && setup.PlayerGroups.TryGetValue(playerId, out var groupIndex))
            return groupIndex;

        for (var i = 0; i < setup.PlayerIds.Count; i++)
            if (setup.PlayerIds[i] == playerId)
                return i;

        throw new ArgumentException($"Player {playerId} is not part of this GameSetup.", nameof(playerId));
    }
}
