using ErrorOr;

namespace Barrelo.Domain.Errors;

public static class PlayerErrors
{
    public static Error NameRequired => Error.Validation(
        "Player.NameRequired",
        "A player name is required.");

    public static Error NotFound => Error.NotFound(
        "Player.NotFound",
        "No player was found with the given id.");
}
