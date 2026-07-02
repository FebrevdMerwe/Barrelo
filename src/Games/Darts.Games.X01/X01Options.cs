namespace Darts.Games.X01;

internal sealed record X01Options(int StartingScore, bool DoubleOut, int LegsToWin, int SetsToWin)
{
    public static X01Options Parse(IReadOnlyDictionary<string, string> options) => new(
        StartingScore: GetInt(options, "startingScore", 501),
        DoubleOut: GetBool(options, "doubleOut", true),
        LegsToWin: GetInt(options, "legsToWin", 3),
        SetsToWin: GetInt(options, "setsToWin", 1));

    private static int GetInt(IReadOnlyDictionary<string, string> options, string key, int defaultValue)
        => options.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : defaultValue;

    private static bool GetBool(IReadOnlyDictionary<string, string> options, string key, bool defaultValue)
        => options.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : defaultValue;
}
