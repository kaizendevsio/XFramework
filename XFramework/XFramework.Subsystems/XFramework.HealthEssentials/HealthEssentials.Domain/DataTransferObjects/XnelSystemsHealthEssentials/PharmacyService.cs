using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class PharmacyService
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public long PharmacyLocationId { get; set; }

    public long PharmacyId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public decimal? Quantity { get; set; }

    public long? UnitId { get; set; }

    public string Guid { get; set; } = null!;

    public virtual PharmacyServiceEntity Entity { get; set; } = null!;

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual PharmacyLocation PharmacyLocation { get; set; } = null!;

    public virtual ICollection<PharmacyServiceTag> PharmacyServiceTags { get; } = new List<PharmacyServiceTag>();

    public virtual Unit? Unit { get; set; }
}
