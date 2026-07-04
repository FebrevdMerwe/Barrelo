using Barrelo.GameSdk;
using ErrorOr;

namespace Barrelo.Application.Common.Interfaces.Services;

public interface IGameCatalog
{
    IReadOnlyList<GameDescriptor> ListAvailable();

    ErrorOr<IGameFactory> Resolve(string gameId);
}
