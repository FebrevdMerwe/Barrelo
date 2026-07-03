using ErrorOr;

namespace Darts.Domain.Errors;

public static class MatchErrors
{
    public static Error GameIdRequired => Error.Validation(
        "Match.GameIdRequired",
        "A game id is required to start a match.");

    public static Error NoParticipants => Error.Validation(
        "Match.NoParticipants",
        "A match requires at least one participant.");

    public static Error AlreadyCompleted => Error.Validation(
        "Match.AlreadyCompleted",
        "The match has already completed.");

    public static Error NotFound => Error.NotFound(
        "Match.NotFound",
        "No match was found with the given id.");

    public static Error GroupAssignmentMismatch => Error.Validation(
        "Match.GroupAssignmentMismatch",
        "Group assignments must be provided for every participant.");
}
