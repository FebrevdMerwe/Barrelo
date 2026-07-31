namespace Barrelo.Api.Contracts;

/// <summary>Posted by a client-owned game's browser UI when its own rules say the match is over.
/// FinalStandings is every participant ordered best-first.</summary>
public sealed record ReportMatchResultRequest(
    IReadOnlyList<Guid> WinnerPlayerIds,
    IReadOnlyList<Guid> FinalStandings);
