using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class GatewayResponseType
{
    public GatewayResponseType()
    {
        GatewayResponses = new HashSet<GatewayResponse>();
    }

    public long Id { get; set; }
    public string Guid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public long GatewayEntityId { get; set; }

    public virtual GatewayEntity GatewayEntity { get; set; }
    public virtual ICollection<GatewayResponse> GatewayResponses { get; set; }
}