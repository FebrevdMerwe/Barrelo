using System.Text.Json;
using System.Text.Json.Serialization;

namespace Barrelo.Api.IntegrationTests;

/// <summary>Mirrors Program.cs's ConfigureHttpJsonOptions so test-side (de)serialization of enums (as strings) matches the server.</summary>
internal static class JsonTestOptions
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
