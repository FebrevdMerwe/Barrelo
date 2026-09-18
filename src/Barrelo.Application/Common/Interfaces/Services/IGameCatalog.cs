using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Common.Interfaces.Services;

public interface IGameCatalog
{
    IReadOnlyList<GameDescriptor> ListAvailable();

    ErrorOr<IGameFactory> Resolve(string gameId);

    /// <summary>True for a game contributed by a client-owned plugin.json manifest — the only kind that can
    /// be installed/removed through the games-management UI at runtime. False for a built-in .NET plugin,
    /// and false for a gameId that isn't registered at all.</summary>
    bool IsClientOwned(string gameId);
}
