namespace Darts.GameSdk;

/// <summary>Thrown by a plugin on malformed input. The host catches this at the command-handler boundary and turns it into an ErrorOr failure instead of crashing.</summary>
public sealed class GameRuleViolationException : Exception
{
    public GameRuleViolationException(string message) : base(message)
    {
    }
}
