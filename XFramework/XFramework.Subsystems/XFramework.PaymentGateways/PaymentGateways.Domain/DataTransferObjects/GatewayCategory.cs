using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class GatewayCategory
{
    public GatewayCategory()
    {
        Gateways = new HashSet<Gateway>();
    }

    public long Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public string Guid { get; set; }

    public virtual ICollection<Gateway> Gateways { get; set; }
}