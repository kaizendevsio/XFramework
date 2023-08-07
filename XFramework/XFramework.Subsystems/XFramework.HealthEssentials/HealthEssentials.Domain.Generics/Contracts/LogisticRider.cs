﻿namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LogisticRider
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid CredentialId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public short Status { get; set; }

    
    public string? VehicleType { get; set; }

    public string? PlateNumber { get; set; }

    public string? LicenseNumber { get; set; }

    public DateTime? LicenseExpiry { get; set; }

    public virtual ICollection<LogisticJobOrder> LogisticJobOrders { get; } = new List<LogisticJobOrder>();

    public virtual ICollection<LogisticRiderHandle> LogisticRiderHandles { get; } = new List<LogisticRiderHandle>();

    public virtual ICollection<LogisticRiderTag> LogisticRiderTags { get; } = new List<LogisticRiderTag>();
}
