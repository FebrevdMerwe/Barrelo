using Barrelo.Application.Common.Dispatch;
using Barrelo.Application.Common.GameExecution;
using ErrorOr;

namespace Barrelo.Application.Commands.Matches.ReportMatchResult;

/// <summary>The outcome of a client-owned game, reported by its browser UI once its own rules say the
/// match is over. FinalStandings is every participant ordered best-first — it's what the session
/// leaderboard awards placement points from.</summary>
public sealed record ReportMatchResultCommand(
    IReadOnlyList<Guid> WinnerPlayerIds,
    IReadOnlyList<Guid> FinalStandings) : IRequest<ErrorOr<MatchStateSnapshotDto>>;
