using ErrorOr;

namespace Barrelo.Application.Common.Errors;

public static class GameErrors
{
    public static Error GameNotFound(string gameId) => Error.NotFound(
        "Game.NotFound",
        $"No game is registered with id '{gameId}'.");

    public static Error InvalidPackage(string reason) => Error.Validation(
        "Game.InvalidPackage",
        reason);

    public static Error GameIdConflict(string gameId) => Error.Conflict(
        "Game.GameIdConflict",
        $"Game id '{gameId}' is already used by a built-in game and cannot be replaced.");

    public static Error CannotRemoveBuiltIn(string gameId) => Error.Forbidden(
        "Game.CannotRemoveBuiltIn",
        $"'{gameId}' is a built-in game and cannot be removed.");
}
