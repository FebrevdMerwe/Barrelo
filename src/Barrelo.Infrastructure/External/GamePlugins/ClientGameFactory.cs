using Barrelo.GameSdk;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>
/// IGameFactory for a client-owned game. Both Describe() and Create() are pure bookkeeping — there is no
/// process to spawn, no port to allocate and no health check to wait on, so starting a match is instant
/// and cannot fail for environmental reasons.
/// </summary>
public sealed class ClientGameFactory(PluginManifest manifest) : IGameFactory
{
    public GameDescriptor Describe() => manifest.ToDescriptor();

    public Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        if (setup.PlayerIds.Count == 0)
            throw new GameRuleViolationException($"Game '{manifest.GameId}' requires at least one player.");

        // The seed is minted here, host-side, so every client replaying this match's log draws the same
        // "random" values. A game that rolls its own randomness will desync its screens instead.
        return Task.FromResult<IGame>(new ClientOwnedGame(manifest.GameId, Random.Shared.Next(), setup));
    }
}
