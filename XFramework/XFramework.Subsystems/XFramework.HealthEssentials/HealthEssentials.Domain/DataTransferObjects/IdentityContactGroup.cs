using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects;

public partial class IdentityContactGroup
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public virtual ICollection<IdentityContact> IdentityContacts { get; } = new List<IdentityContact>();
}
