namespace Darts.GameSdk;

/// <summary>Ordered player list plus a loosely-typed options blob that only the plugin interprets.
/// <see cref="PlayerGroups"/> is optional — a missing entry (or a null dictionary) means that player
/// is implicitly its own singleton group, which is exactly today's per-player behavior. Use
/// <see cref="GameSetupExtensions.EffectiveGroupIndex"/> to resolve a player's group consistently.</summary>
public sealed record GameSetup(
    IReadOnlyList<Guid> PlayerIds,
    IReadOnlyDictionary<string, string> Options,
    IReadOnlyDictionary<Guid, int>? PlayerGroups = null);
