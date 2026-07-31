using Barrelo.Application.Common.Dispatch;
using ErrorOr;

namespace Barrelo.Application.Commands.Matches.ReportReplayHash;

/// <summary>One client's report that replaying the log identified by LogHash produced the state
/// identified by StateHash. Used only to detect two clients disagreeing — see
/// IReplayDivergenceMonitor.</summary>
public sealed record ReportReplayHashCommand(string LogHash, string StateHash) : IRequest<ErrorOr<Success>>;
