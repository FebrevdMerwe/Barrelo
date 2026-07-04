namespace Barrelo.Games.Kickoff;

internal sealed record KickoffOptions(int GoalsToWinLeg, int LegsToWinMatch)
{
    public static KickoffOptions Parse(IReadOnlyDictionary<string, string> options) => new(
        GoalsToWinLeg: GetInt(options, "goalsToWinLeg", 3),
        LegsToWinMatch: GetInt(options, "legsToWinMatch", 2));

    private static int GetInt(IReadOnlyDictionary<string, string> options, string key, int defaultValue)
        => options.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : defaultValue;
}
