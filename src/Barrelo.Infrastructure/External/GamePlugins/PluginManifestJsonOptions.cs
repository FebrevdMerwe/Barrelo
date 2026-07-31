using System.Text.Json;
using System.Text.Json.Serialization;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Shared JSON conventions for parsing a game's plugin.json — camelCase properties, enums as
/// strings, matching the conventions already applied to the rest of the platform's wire formats
/// (Program.cs's HTTP/SignalR JsonStringEnumConverter).</summary>
internal static class PluginManifestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
