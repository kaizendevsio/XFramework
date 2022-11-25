using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class LogisticRider
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string CredentialGuid { get; set; } = null!;

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public short Status { get; set; }

    public string Guid { get; set; } = null!;

    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }

    public string? LicenseNumber { get; set; }

    public DateTime? LicenseExpiry { get; set; }

    public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; } = new List<LogisticJobOrder>();

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; } = new List<LogisticRiderHandle>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; } = new List<LogisticRiderTag>();
}
