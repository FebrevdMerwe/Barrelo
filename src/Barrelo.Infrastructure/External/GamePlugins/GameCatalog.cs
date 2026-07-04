using Barrelo.Application.Common.Errors;
using Barrelo.Application.Common.Interfaces.Services;
using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Aggregates IGameFactory instances contributed by whichever loader(s) are registered — today just PluginGameLoader.</summary>
public sealed class GameCatalog : IGameCatalog
{
    private readonly Dictionary<string, IGameFactory> _factoriesByGameId;

    public GameCatalog(IEnumerable<IGameFactory> factories)
    {
        _factoriesByGameId = factories.ToDictionary(f => f.Describe().GameId);
    }

    public IReadOnlyList<GameDescriptor> ListAvailable() =>
        _factoriesByGameId.Values.Select(f => f.Describe()).ToArray();

    public ErrorOr<IGameFactory> Resolve(string gameId) =>
        _factoriesByGameId.TryGetValue(gameId, out var factory)
            ? ErrorOrFactory.From(factory)
            : GameErrors.GameNotFound(gameId);
}
