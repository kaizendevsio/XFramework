using System.Text.Json.Serialization;

namespace HealthEssentials.Domain.Contracts.Responses;

public class MimsAutoCompleteResponse
{
    [JsonPropertyName("query")]
    public string? Query { get; set; }

    [JsonPropertyName("suggestions")]
    public List<Suggestion>? Suggestions { get; set; }
    
    public class Suggestion
    {
        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }
}