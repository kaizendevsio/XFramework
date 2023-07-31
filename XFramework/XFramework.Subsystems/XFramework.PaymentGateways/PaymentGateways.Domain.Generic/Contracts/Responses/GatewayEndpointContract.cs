using System;

namespace PaymentGateways.Domain.Generic.Contracts.Responses;

public class GatewayEndpointContract
{
    public long Id { get; set; }
    public long? GatewayId { get; set; }
    public string Name { get; set; }
    public string BaseUrlEndpoint { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool? IsDeleted { get; set; }
    public string UrlEndpoint { get; set; }
}