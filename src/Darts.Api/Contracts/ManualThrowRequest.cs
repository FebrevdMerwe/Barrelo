using Darts.GameSdk;

namespace Darts.Api.Contracts;

public sealed record ManualThrowRequest(int Segment, Ring Ring);
