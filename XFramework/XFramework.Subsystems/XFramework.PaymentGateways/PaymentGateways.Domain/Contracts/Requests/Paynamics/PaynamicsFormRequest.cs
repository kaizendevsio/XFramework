using System.Text.Json.Serialization;

namespace PaymentGateways.Domain.Contracts.Requests.Paynamics;

public class PaynamicsFormRequest
{
    [JsonPropertyName("paymentrequest")] public string PaymentRequest { get; set; }
}