using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.BusinessObjects.TOC;

public class TocAppId
{
    [JsonPropertyName("appId")]
    public string AppId { get; set; }
}