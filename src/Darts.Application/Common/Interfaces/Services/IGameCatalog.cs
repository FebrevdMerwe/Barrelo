using Darts.GameSdk;
using ErrorOr;

namespace Darts.Application.Common.Interfaces.Services;

public interface IGameCatalog
{
    IReadOnlyList<GameDescriptor> ListAvailable();

    ErrorOr<IGameFactory> Resolve(string gameId);
}
