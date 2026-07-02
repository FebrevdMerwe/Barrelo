namespace Darts.GameSdk;

/// <summary>Ordered player list plus a loosely-typed options blob that only the plugin interprets.</summary>
public sealed record GameSetup(
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string> Options);
