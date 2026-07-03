using System.Text.Json.Serialization;

namespace Darts.GameSdk;

/// <summary>Base for declarative, additive setting kinds a plugin can expose via <see cref="GameDescriptor.Settings"/>.
/// New kinds are added by adding a new derived record + JsonDerivedType entry — never by changing this contract.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(GameModeSetting), "gameMode")]
[JsonDerivedType(typeof(PlayerGroupSetting), "playerGroup")]
public abstract record GameSettingDefinition
{
    /// <summary>Excluded from JSON — the polymorphic type discriminator already serializes as "kind".</summary>
    [JsonIgnore]
    public abstract string Kind { get; }
}

/// <summary>A single named choice within a <see cref="GameModeSetting"/>. <see cref="Options"/> is merged
/// into <see cref="GameSetup.Options"/> verbatim when this choice is selected — no new plugin method,
/// reuses the existing options dict mechanism.</summary>
public sealed record GameModeChoice(
    string Value,
    string DisplayName,
    IReadOnlyDictionary<string, string> Options);

public sealed record GameModeSetting(
    string Key,
    string DisplayName,
    IReadOnlyList<GameModeChoice> Choices,
    string DefaultValue) : GameSettingDefinition
{
    [JsonIgnore]
    public override string Kind => "gameMode";
}

/// <summary>Declares that this game requires players to be split into a fixed number of ephemeral,
/// match-only groups (teams). Groups are mandatory once declared; <see cref="MaxGroups"/> is a fixed
/// bucket count (not a range) and each bucket may hold 0..<see cref="MaxPlayersPerGroup"/> players.</summary>
public sealed record PlayerGroupSetting(
    string Key,
    string DisplayName,
    int MaxGroups,
    int MaxPlayersPerGroup) : GameSettingDefinition
{
    [JsonIgnore]
    public override string Kind => "playerGroup";
}
