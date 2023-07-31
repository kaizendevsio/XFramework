using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class GatewayEndpoint
{
    public GatewayEndpoint()
    {
        Gateways = new HashSet<Gateway>();
    }

    public long Id { get; set; }
    public long? GatewayId { get; set; }
    public string Name { get; set; }
    public string BaseUrlEndpoint { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool? IsDeleted { get; set; }
    public string UrlEndpoint { get; set; }
    public string Guid { get; set; }

    public virtual GatewayEntity Gateway { get; set; }
    public virtual ICollection<Gateway> Gateways { get; set; }
}