using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects;

public partial class Gateway
{
    public Gateway()
    {
        DepositRequests = new HashSet<DepositRequest>();
        GatewayInstructions = new HashSet<GatewayInstruction>();
    }

    public long Id { get; set; }
    public long GatewayCategoryId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public decimal ServiceCharge { get; set; }
    public string Image { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool? IsDeleted { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ProviderEndpointId { get; set; }
    public decimal? Discount { get; set; }
    public decimal ConvenienceFee { get; set; }
    public string Guid { get; set; }

    public virtual GatewayCategory GatewayCategory { get; set; }
    public virtual GatewayEndpoint ProviderEndpoint { get; set; }
    public virtual ICollection<DepositRequest> DepositRequests { get; set; }
    public virtual ICollection<GatewayInstruction> GatewayInstructions { get; set; }
}