using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Requests.TOC;

public class TocApiRequest
{
    [JsonPropertyName("data")]
    public string Data { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }
}