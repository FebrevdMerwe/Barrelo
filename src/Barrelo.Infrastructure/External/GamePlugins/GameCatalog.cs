using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Aggregates IGameFactory instances contributed by whichever loader(s) are registered. Built-in
/// (.NET plugin) factories are fixed at construction — they live in a collectible AssemblyLoadContext that
/// is never unloaded while the process runs. Client-owned factories can be swapped out at runtime via
/// <see cref="ReloadClientGames"/> since they're just data (a plugin.json manifest), letting the
/// games-management UI install/remove them without a restart.</summary>
public sealed class GameCatalog : IGameCatalog
{
    private readonly IReadOnlyDictionary<string, IGameFactory> _builtInFactoriesByGameId;
    private volatile IReadOnlyDictionary<string, IGameFactory> _clientFactoriesByGameId =
        new Dictionary<string, IGameFactory>();

    public GameCatalog(IEnumerable<IGameFactory> factories)
    {
        _builtInFactoriesByGameId = factories.ToDictionary(f => f.Describe().GameId);
    }

    /// <summary>Replaces the entire set of client-owned games with a freshly rescanned list. A factory
    /// whose gameId collides with a built-in one is dropped — a built-in game can never be shadowed.</summary>
    public void ReloadClientGames(IEnumerable<IGameFactory> factories)
    {
        _clientFactoriesByGameId = factories
            .Where(f => !_builtInFactoriesByGameId.ContainsKey(f.Describe().GameId))
            .ToDictionary(f => f.Describe().GameId);
    }

    public IReadOnlyList<GameDescriptor> ListAvailable() =>
        _builtInFactoriesByGameId.Values.Concat(_clientFactoriesByGameId.Values).Select(f => f.Describe()).ToArray();

    public ErrorOr<IGameFactory> Resolve(string gameId)
    {
        if (_builtInFactoriesByGameId.TryGetValue(gameId, out var builtIn))
            return ErrorOrFactory.From(builtIn);

        return _clientFactoriesByGameId.TryGetValue(gameId, out var clientOwned)
            ? ErrorOrFactory.From(clientOwned)
            : GameErrors.GameNotFound(gameId);
    }

    public bool IsClientOwned(string gameId) => _clientFactoriesByGameId.ContainsKey(gameId);
}
