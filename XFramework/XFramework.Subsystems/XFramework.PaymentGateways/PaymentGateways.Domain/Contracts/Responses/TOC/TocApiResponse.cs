using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Responses.TOC;

public class TocApiResponse
{
    [JsonPropertyName("version")]
    public string Version { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("result")]
    public object Result { get; set; }
}