using System;
using System.Collections.Generic;

namespace PaymentGateways.Domain.Generic.Contracts.Responses;

public class GatewayCategoryContract
{
    public long Id { get; set; }
    public long ProviderEntityId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public DateTime? ModifiedAt { get; set; }
        
    public virtual ICollection<GatewayContract> Gateways { get; set; }
}