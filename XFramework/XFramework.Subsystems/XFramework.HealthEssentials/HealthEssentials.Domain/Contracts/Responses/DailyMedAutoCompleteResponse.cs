using System.Text.Json.Serialization;

namespace HealthEssentials.Domain.Contracts.Responses;

public class DailyMedAutoCompleteResponse
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}