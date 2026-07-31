using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Interfaces.Services;

/// <summary>
/// Implemented by a game whose rules run outside the host, so the host cannot work out who won on its
/// own and is told instead. Deliberately not part of IGame in Barrelo.GameSdk: an in-process plugin
/// determines its own result and must never have a result pushed into it.
/// </summary>
public interface IClientReportedGame
{
    /// <summary>Records the final outcome and marks the game complete. The caller is responsible for
    /// validating that the reported ids belong to this match.</summary>
    Task ReportResult(GameResult result, CancellationToken ct);
}
