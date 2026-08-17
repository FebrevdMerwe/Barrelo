namespace Barrelo.GameSdk;

/// <summary>What the host needs to list a game and shape its start-match form, without instantiating it.</summary>
/// <param name="MinPlayers">Fewest players the game can be started with. Defaults to 1 — a game is
/// solo-playable unless it says otherwise, so practice-friendly games need no ceremony and only games that
/// genuinely need an opponent (or a full team per goal) opt into a higher floor. The host enforces this
/// when a match is started; a game may still reject a roster in <see cref="IGameFactory.Create"/> for
/// reasons this number can't express.</param>
public sealed record GameDescriptor(
    string GameId,
    string DisplayName,
    string Description,
    IReadOnlyList<GameSettingDefinition> Settings,
    int MinPlayers = 1);
