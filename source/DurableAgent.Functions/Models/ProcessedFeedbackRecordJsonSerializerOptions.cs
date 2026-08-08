using System.Text.Json;
using System.Text.Json.Serialization;

namespace DurableAgent.Functions.Models;

/// <summary>
/// Shared JSON serializer options for persisted feedback records.
/// </summary>
public static class ProcessedFeedbackRecordJsonSerializerOptions
{
    /// <summary>Default serializer options for processed feedback record blobs.</summary>
    public static JsonSerializerOptions Default { get; } = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}
