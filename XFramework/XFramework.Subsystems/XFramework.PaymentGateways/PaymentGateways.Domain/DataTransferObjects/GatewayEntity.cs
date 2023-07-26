using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class GatewayEntity
{
    public GatewayEntity()
    {
        GatewayEndpoints = new HashSet<GatewayEndpoint>();
        GatewayResponseTypes = new HashSet<GatewayResponseType>();
    }

    public long Id { get; set; }
    public string Guid { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public DateTime? ModifiedAt { get; set; }

    public virtual ICollection<GatewayEndpoint> GatewayEndpoints { get; set; }
    public virtual ICollection<GatewayResponseType> GatewayResponseTypes { get; set; }
}