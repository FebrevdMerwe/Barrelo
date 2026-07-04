using Barrelo.GameSdk;

namespace Barrelo.Games.Kickoff.UnitTests;

internal static class KickoffTestGame
{
    public static async Task<IGame> Create(
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<Guid, int>? playerGroups = null,
        IReadOnlyDictionary<string, string>? options = null)
    {
        var setup = new GameSetup(players, options ?? new Dictionary<string, string>(), playerGroups);
        return await new KickoffGameFactory().Create(setup, CancellationToken.None);
    }

    public static async Task<KickoffStatePayload> Payload(this IGame game)
    {
        var state = await game.GetState();
        return (KickoffStatePayload)state.Payload!;
    }

    public static KickoffGroupScore GroupFor(this KickoffStatePayload payload, Guid playerId) =>
        payload.Groups.Single(g => g.PlayerIds.Contains(playerId));

    /// <summary>Goals are scored east/west. Segment 6 sits at angle 90deg (due "east"), so a Double
    /// (0.5 magnitude) kick from center lands exactly at newX = 0.5 + 0.5 = 1, inside the goal mouth
    /// (y unchanged at 0.5) — scores at the east goal (owner 0, the right-hand goal on screen).</summary>
    public static DetectedThrow EastGoalKick() => TestThrow.Of(Ring.Double, 6);

    /// <summary>Segment 11 sits at angle 270deg (due "west"), so a Double kick from center lands
    /// exactly at newX = 0.5 - 0.5 = 0, inside the goal mouth — scores at the west goal (owner 1, the
    /// left-hand goal on screen).</summary>
    public static DetectedThrow WestGoalKick() => TestThrow.Of(Ring.Double, 11);

    /// <summary>Segment 20 sits at angle 0deg (due "north", BoardGeometry's segment order starts
    /// there), so a Double kick from center lands exactly on the touchline (newY = 0.5 - 0.5 = 0),
    /// which is still in play — a second NorthKick from there pushes past it (newY = -0.5) for a
    /// guaranteed out-of-bounds.</summary>
    public static DetectedThrow NorthKick() => TestThrow.Of(Ring.Double, 20);
}
