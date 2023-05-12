using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.BusinessObjects.TOC;

public class TocAuthCredentials
{
    [JsonPropertyName("password")]
    public string Password { get; set; }

    [JsonPropertyName("userName")]
    public string UserName { get; set; }
}