using System.Text.Json;
using System.Text.Json.Serialization;

namespace Barrelo.Infrastructure.External.GamePlugins;

/// <summary>Shared JSON conventions for talking to an out-of-process game and for parsing its plugin.json —
/// camelCase properties, enums as strings, matching the conventions already applied to the rest of the
/// platform's wire formats (Program.cs's HTTP/SignalR JsonStringEnumConverter).</summary>
internal static class RemoteGameJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
