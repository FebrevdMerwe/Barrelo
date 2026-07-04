using Microsoft.Extensions.Configuration;

namespace Barrelo.Infrastructure.External.GamePlugins;

public static class PluginsDirectoryResolver
{
    public static string Resolve(IConfiguration configuration)
    {
        var configured = configuration["Plugins:Directory"] ?? "plugins";
        return Path.IsPathRooted(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
    }
}
