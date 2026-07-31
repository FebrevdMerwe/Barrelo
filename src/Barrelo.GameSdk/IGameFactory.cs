namespace Barrelo.GameSdk;

/// <summary>Split from <see cref="IGame"/> so the host can list available games without instantiating one.</summary>
public interface IGameFactory
{
    GameDescriptor Describe();

    /// <summary>Async so a factory that has real work to do before the game is ready isn't forced to block. In-process factories just Task.FromResult.</summary>
    Task<IGame> Create(GameSetup setup, CancellationToken ct);
}
