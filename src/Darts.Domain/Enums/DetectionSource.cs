namespace Darts.Domain.Enums;

/// <summary>Domain's own mirror of Darts.GameSdk.DetectionSourceType — Domain must not reference GameSdk.</summary>
public enum DetectionSource
{
    AutoDarts,
    Mock,
    Manual,
}
