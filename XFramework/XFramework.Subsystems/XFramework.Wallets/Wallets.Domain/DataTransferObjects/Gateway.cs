using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects;

public partial class Gateway
{
    public long Id { get; set; }

    public long GatewayCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public decimal ServiceCharge { get; set; }

    public string? Image { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ProviderEndpointId { get; set; }

    public decimal? Discount { get; set; }

    public decimal ConvenienceFee { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<DepositRequest> DepositRequests { get; } = new List<DepositRequest>();

    public virtual GatewayCategory GatewayCategory { get; set; } = null!;

    public virtual GatewayEndpoint? ProviderEndpoint { get; set; }
}
