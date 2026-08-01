using Barrelo.GameSdk;

namespace Barrelo.Application.Common.Notifications;

/// <summary>What a screen needs in order to say something true about the board: which detector this host
/// is configured for, and whether it is reachable right now. Deliberately says nothing detector-specific
/// — a screen renders the same two fields whatever is plugged in.</summary>
public sealed record DetectionStatus(DetectionSourceType Source, bool IsConnected);
