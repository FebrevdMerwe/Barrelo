namespace Darts.Games.X01;

internal sealed class X01PlayerState(Guid playerId, int startingScore)
{
    public Guid PlayerId { get; } = playerId;
    public int RemainingScore { get; set; } = startingScore;
    public int LegsWon { get; set; }
    public int SetsWon { get; set; }
}
