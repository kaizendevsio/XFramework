using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class VendorEntity
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string Guid { get; set; } = null!;

    public long GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual VendorEntityGroup Group { get; set; } = null!;

    public virtual ICollection<Vendor> Vendors { get; } = new List<Vendor>();
}
