using System.Collections.Concurrent;
using Barrelo.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.Sessions;

/// <summary>
/// In-memory and advisory: it never rejects a report, it only logs when two clients disagree. Not
/// persisted across process restart — same v1 limitation as the other session stores.
///
/// Only one match is ever active at a time, so a report for a new match id is proof that every earlier
/// match is over and its hashes can go. That keeps the store bounded without anyone having to remember
/// to call a cleanup hook on every path a match can end by.
/// </summary>
public sealed class ReplayDivergenceMonitor(ILogger<ReplayDivergenceMonitor> logger) : IReplayDivergenceMonitor
{
    private readonly ConcurrentDictionary<string, string> _stateHashByLogHash = new();
    private readonly ConcurrentDictionary<string, byte> _alreadyWarned = new();
    private readonly Lock _matchGate = new();
    private Guid _currentMatchId;

    public void Report(Guid matchId, string logHash, string stateHash)
    {
        DropEarlierMatch(matchId);

        var reference = _stateHashByLogHash.GetOrAdd(logHash, stateHash);
        if (string.Equals(reference, stateHash, StringComparison.Ordinal))
            return;

        // One warning per diverging input, not one per client per push — a desynced match would
        // otherwise flood the log on every subsequent dart.
        if (!_alreadyWarned.TryAdd(logHash, 0))
            return;

        logger.LogWarning(
            "Replay divergence in match {MatchId}: two clients replayed the same log (hash '{LogHash}') but " +
            "derived different states ('{ReferenceHash}' vs '{ReportedHash}'). The game's replay is not " +
            "deterministic — check for Math.random(), Date.now() or crypto.randomUUID() in its rules, which " +
            "must derive from payload.seed and throw.detectedAtUtc instead.",
            matchId, logHash, reference, stateHash);
    }

    private void DropEarlierMatch(Guid matchId)
    {
        lock (_matchGate)
        {
            if (_currentMatchId == matchId)
                return;

            _currentMatchId = matchId;
            _stateHashByLogHash.Clear();
            _alreadyWarned.Clear();
        }
    }
}
