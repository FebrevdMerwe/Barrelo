namespace Barrelo.GameSdk;

public sealed record GameDescriptor(
    string GameId,
    string DisplayName,
    string Description,
    IReadOnlyList<GameSettingDefinition> Settings);
