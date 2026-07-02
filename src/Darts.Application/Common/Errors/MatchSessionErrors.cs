using ErrorOr;

namespace Darts.Application.Common.Errors;

public static class MatchSessionErrors
{
    public static Error SessionNotFound(Guid matchId) => Error.NotFound(
        "Match.SessionNotFound",
        $"No active session exists for match '{matchId}'.");

    public static Error BoardNotBound(string boardId) => Error.NotFound(
        "Match.BoardNotBound",
        $"No active match is bound to board '{boardId}'.");
}
