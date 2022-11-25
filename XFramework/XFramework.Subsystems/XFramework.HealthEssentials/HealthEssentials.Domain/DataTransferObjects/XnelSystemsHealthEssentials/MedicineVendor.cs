using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class MedicineVendor
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long MedicineId { get; set; }

    public long VendorId { get; set; }

    public string Guid { get; set; } = null!;

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
