using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>
/// IGame proxy for an out-of-process game: one Process + one HttpClient per match, both owned for the
/// match's whole lifetime. Pull-based like every other IGame — the remote process never calls back in,
/// it only answers requests — so this fits the existing host/plugin contract unchanged.
///
/// Connectivity failure (the process crashed or stopped responding) is swallowed, not thrown: there is no
/// caller on the other end of GameCommandExecutor's per-matchId lock that can usefully retry, and the match
/// still needs a snapshot to hand to the scoreboard. So the last snapshot successfully pulled from the
/// process is cached and replayed with Status=Aborted once connectivity is lost, instead of surfacing an
/// exception up through the dispatcher.
/// </summary>
public sealed class RemoteGame(Process process, HttpClient http, ILogger logger) : IGame, IAsyncDisposable
{
    private GameStateSnapshot? _lastKnownSnapshot;
    private bool _aborted;

    public Task ReceiveThrow(DetectedThrow detectedThrow, CancellationToken ct) =>
        Call(() => http.PostAsJsonAsync("/throw", detectedThrow, RemoteGameJsonOptions.Default, ct));

    public Task ReceiveEndOfTurn(CancellationToken ct) =>
        Call(() => http.PostAsync("/end-turn", content: null, ct));

    public Task UndoLastThrow(CancellationToken ct) =>
        Call(() => http.PostAsync("/undo", content: null, ct));

    public bool IsComplete => !_aborted && (_lastKnownSnapshot?.IsComplete ?? false);

    public async Task<GameStateSnapshot> GetState()
    {
        if (_aborted)
            return Aborted();

        try
        {
            var snapshot = await http.GetFromJsonAsync<GameStateSnapshot>("/state", RemoteGameJsonOptions.Default);
            if (snapshot is not null)
                _lastKnownSnapshot = snapshot;
            return snapshot ?? Aborted();
        }
        catch (Exception ex) when (IsConnectivityFailure(ex))
        {
            MarkAborted(ex);
            return Aborted();
        }
    }

    public async Task<GameResult> GetResult()
    {
        var result = await http.GetFromJsonAsync<GameResult>("/result", RemoteGameJsonOptions.Default);
        return result ?? new GameResult([], []);
    }

    private async Task Call(Func<Task<HttpResponseMessage>> send)
    {
        if (_aborted)
            return;

        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (Exception ex) when (IsConnectivityFailure(ex))
        {
            MarkAborted(ex);
            return;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new GameRuleViolationException(message);
        }

        response.EnsureSuccessStatusCode();
    }

    private void MarkAborted(Exception ex)
    {
        _aborted = true;
        logger.LogWarning(ex, "Lost connectivity to remote game process (pid {Pid}); marking match aborted.", SafeProcessId());
    }

    private GameStateSnapshot Aborted() =>
        (_lastKnownSnapshot ?? new GameStateSnapshot(Guid.Empty, "", GameStatus.Aborted, null, 0, 0, [], false, null, null))
        with
        { Status = GameStatus.Aborted };

    private int? SafeProcessId()
    {
        try
        {
            return process.Id;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Anything that means "this process can no longer be trusted to drive the match" — dropped
    /// connections/timeouts, but also a malformed/incompatible response body. Either way there is no
    /// caller who can usefully retry, so this degrades to Aborted rather than throwing.</summary>
    private static bool IsConnectivityFailure(Exception ex) =>
        ex is HttpRequestException or TaskCanceledException or System.Net.Sockets.SocketException or JsonException;

    public async ValueTask DisposeAsync()
    {
        http.Dispose();
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited, or exiting concurrently — nothing left to clean up.
        }

        process.Dispose();
        await Task.CompletedTask;
    }
}
