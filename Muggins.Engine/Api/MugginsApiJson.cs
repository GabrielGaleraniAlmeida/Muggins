using System.Text.Json;

namespace Muggins.Engine.Api;

/// <summary>Serializer settings for the API's camelCase JSON and numeric enums.</summary>
public static class MugginsApiJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
