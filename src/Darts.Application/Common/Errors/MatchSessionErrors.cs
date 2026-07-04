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
}
