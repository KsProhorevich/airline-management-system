using System.Text.Json.Serialization;

namespace AirlineApi.Tests;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    static JsonDefaults()
    {
        Options.Converters.Add(new JsonStringEnumConverter());
    }
}
