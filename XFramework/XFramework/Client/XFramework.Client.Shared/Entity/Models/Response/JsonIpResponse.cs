using System.Text.Json.Serialization;

namespace XFramework.Client.Shared.Entity.Models.Response;

public class JsonIpResponse
{
    [JsonPropertyName("ip")]
    public string IpAddress { get; set; }

    [JsonPropertyName("geo-ip")]
    public string GeoIp { get; set; }

    [JsonPropertyName("API Help")]
    public string APIHelp { get; set; }
}