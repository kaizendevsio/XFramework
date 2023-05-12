using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects;

public partial class GatewayEndpoint
{
    public long Id { get; set; }

    public long? GatewayId { get; set; }

    public string? Name { get; set; }

    public string? BaseUrlEndpoint { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public string? UrlEndpoint { get; set; }

    public string Guid { get; set; } = null!;

    public virtual GatewayEntity? Gateway { get; set; }

    public virtual ICollection<Gateway> Gateways { get; } = new List<Gateway>();
}
