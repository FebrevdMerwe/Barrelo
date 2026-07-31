namespace Barrelo.Api.Contracts;

/// <summary>One client's report that replaying the log it holds produced a given state. Advisory — used
/// only to spot two clients deriving different states from the same log.</summary>
public sealed record ReportReplayHashRequest(string LogHash, string StateHash);
