using Darts.GameSdk;

namespace Darts.Api.Contracts;

public sealed record ManualThrowRequest(string? BoardId, int Segment, Ring Ring);
