using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects;

public partial class GatewayCategory
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public string Guid { get; set; } = null!;

    public virtual ICollection<Gateway> Gateways { get; } = new List<Gateway>();
}
