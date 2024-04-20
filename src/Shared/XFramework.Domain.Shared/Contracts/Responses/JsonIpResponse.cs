using System.Text.Json.Serialization;

namespace XFramework.Domain.Shared.Contracts.Responses;

public class JsonIpResponse
{
    [JsonPropertyName("ip")]
    public string? IpAddress { get; set; }

    [JsonPropertyName("geo-ip")]
    public string? GeoIp { get; set; }

    [JsonPropertyName("API Help")]
    public string? ApiHelp { get; set; }
}