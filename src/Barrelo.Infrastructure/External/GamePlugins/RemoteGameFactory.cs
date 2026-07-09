using System.Diagnostics;
using System.Net.Http.Json;
using Barrelo.GameSdk;
using Microsoft.Extensions.Logging;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>
/// IGameFactory for an out-of-process game. Describe() reads only the manifest — never spawns a process —
/// so every remote game can be listed on the start screen with nothing running yet. Create() spawns one
/// fresh process per match (never shared across matches) and hands back a RemoteGame that owns it for the
/// match's whole lifetime.
/// </summary>
public sealed class RemoteGameFactory(RemoteGameManifest manifest, string pluginDirectory, ILogger logger) : IGameFactory
{
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromMilliseconds(200);

    public GameDescriptor Describe() => manifest.ToDescriptor();

    public async Task<IGame> Create(GameSetup setup, CancellationToken ct)
    {
        var port = FreePortFinder.FindFreePort();
        var process = StartProcess(port);

        var http = new HttpClient
        {
            BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            Timeout = TimeSpan.FromSeconds(10),
        };

        if (!await WaitUntilHealthy(http, process, ct))
        {
            http.Dispose();
            KillQuietly(process);
            throw new GameUnavailableException(
                $"Game '{manifest.GameId}' did not become ready within {manifest.Health.TimeoutSeconds}s.");
        }

        var createResponse = await http.PostAsJsonAsync("/create", setup, RemoteGameJsonOptions.Default, ct);
        if (!createResponse.IsSuccessStatusCode)
        {
            var body = await createResponse.Content.ReadAsStringAsync(ct);
            http.Dispose();
            KillQuietly(process);
            throw new GameUnavailableException($"Game '{manifest.GameId}' rejected match setup: {body}");
        }

        return new RemoteGame(process, http, logger);
    }

    private Process StartProcess(int port)
    {
        var workingDirectory = string.IsNullOrEmpty(manifest.Launch.Cwd)
            ? pluginDirectory
            : Path.Combine(pluginDirectory, manifest.Launch.Cwd);

        var startInfo = new ProcessStartInfo
        {
            FileName = manifest.Launch.Command,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var arg in manifest.Launch.Args)
            startInfo.ArgumentList.Add(arg.Replace("{{port}}", port.ToString()));

        var process = Process.Start(startInfo)
            ?? throw new GameUnavailableException($"Failed to start process for game '{manifest.GameId}'.");

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.LogWarning("[{GameId} stderr] {Line}", manifest.GameId, e.Data);
        };
        process.BeginErrorReadLine();

        return process;
    }

    private async Task<bool> WaitUntilHealthy(HttpClient http, Process process, CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(manifest.Health.TimeoutSeconds);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (process.HasExited)
                return false;

            try
            {
                var response = await http.GetAsync(manifest.Health.Path, ct);
                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Not listening yet — keep polling until the deadline.
            }

            await Task.Delay(HealthPollInterval, ct);
        }

        return false;
    }

    private static void KillQuietly(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Already exited — nothing to do.
        }
        finally
        {
            process.Dispose();
        }
    }
}
