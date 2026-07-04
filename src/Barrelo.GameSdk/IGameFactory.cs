namespace Barrelo.GameSdk;

/// <summary>Split from <see cref="IGame"/> so the host can list available games without instantiating one.</summary>
public interface IGameFactory
{
    GameDescriptor Describe();

    /// <summary>Async so a future out-of-process proxy can spawn/attach to a subprocess before the game is ready. In-process factories just Task.FromResult.</summary>
    Task<IGame> Create(GameSetup setup, CancellationToken ct);
}
