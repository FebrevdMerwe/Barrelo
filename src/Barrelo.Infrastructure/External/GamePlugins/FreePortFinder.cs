using System.Net;
using System.Net.Sockets;

namespace Barrelo.Infrastructure.External.GamePlugins;

internal static class FreePortFinder
{
    /// <summary>Binds to an OS-assigned loopback port, then releases it immediately for the spawned
    /// process to bind — a small TOCTOU window, acceptable for a single-machine, low-contention host.</summary>
    public static int FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
