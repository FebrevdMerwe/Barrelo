using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Leaderboard.ResetLeaderboard;

/// <summary>Clears session leaderboard points/standings only — leaves ISessionPlayerStore and the
/// permanent roster untouched.</summary>
public sealed record ResetLeaderboardCommand : IRequest<ErrorOr<Success>>;
