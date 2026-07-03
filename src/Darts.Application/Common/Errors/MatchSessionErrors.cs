using ErrorOr;

namespace Darts.Application.Common.Errors;

public static class MatchSessionErrors
{
    public static Error SessionNotFound(Guid matchId) => Error.NotFound(
        "Match.SessionNotFound",
        $"No active session exists for match '{matchId}'.");

    public static Error NoActiveMatch => Error.NotFound(
        "Match.NoActiveMatch",
        "No match is currently active.");

    public static Error MatchAlreadyActive => Error.Conflict(
        "Match.AlreadyActive",
        "A match is already in progress. Finish it before starting a new one.");
}
