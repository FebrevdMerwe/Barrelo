namespace Barrelo.GameSdk;

/// <summary>
/// Pull-based, never plugin-initiated: the host calls in and pulls state out, the plugin never raises
/// callbacks into host code. This keeps a plugin hosted in a collectible AssemblyLoadContext passive,
/// avoiding cross-boundary delegate references that would block ALC unloading.
/// </summary>
public interface IGame
{
    Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct);

    Task ReceiveEndOfTurn(CancellationToken ct);

    Task UndoLastThrow(CancellationToken ct);

    Task<GameStateSnapshot> GetState();

    bool IsComplete { get; }

    Task<GameResult> GetResult();
}
