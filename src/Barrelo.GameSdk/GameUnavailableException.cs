namespace Barrelo.GameSdk;

/// <summary>Thrown by an <see cref="IGameFactory"/> when the game it describes cannot be created — e.g. an
/// out-of-process game's launch/health-check failed. Unlike <see cref="GameRuleViolationException"/> (thrown
/// by a plugin's own rules), this is an infrastructure failure raised by the loader, but lives alongside it
/// so Application-layer command handlers can catch both at the same boundary.</summary>
public sealed class GameUnavailableException : Exception
{
    public GameUnavailableException(string message) : base(message)
    {
    }
}
