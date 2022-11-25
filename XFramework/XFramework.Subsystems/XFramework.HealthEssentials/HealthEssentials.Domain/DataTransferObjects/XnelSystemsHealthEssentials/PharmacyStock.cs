using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class PharmacyStock
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long PharmacyId { get; set; }

    public long MedicineId { get; set; }

    public DateTime LastRestock { get; set; }

    public long? AvailableQuantity { get; set; }

    public long? CriticalQuantity { get; set; }

    public long? MinQuantity { get; set; }

    public long? MaxQuantity { get; set; }

    public long? Unit { get; set; }

    public string Guid { get; set; } = null!;

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual Unit? UnitNavigation { get; set; }
}
