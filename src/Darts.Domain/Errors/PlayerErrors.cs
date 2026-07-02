using ErrorOr;

namespace Darts.Domain.Errors;

public static class PlayerErrors
{
    public static Error NameRequired => Error.Validation(
        "Player.NameRequired",
        "A player name is required.");
}
