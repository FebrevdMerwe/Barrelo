using ErrorOr;

namespace Darts.Application.Common.Errors;

public static class GameErrors
{
    public static Error GameNotFound(string gameId) => Error.NotFound(
        "Game.NotFound",
        $"No game is registered with id '{gameId}'.");
}
