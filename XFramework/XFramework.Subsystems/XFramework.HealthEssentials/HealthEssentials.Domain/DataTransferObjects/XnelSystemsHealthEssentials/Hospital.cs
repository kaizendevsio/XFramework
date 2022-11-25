using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Hospital
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public string Guid { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Website { get; set; }

    public string? Logo { get; set; }

    public virtual HospitalEntity Entity { get; set; } = null!;

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; } = new List<HospitalConsultation>();

    public virtual ICollection<HospitalLaboratory> HospitalLaboratories { get; } = new List<HospitalLaboratory>();

    public virtual ICollection<HospitalLocation> HospitalLocations { get; } = new List<HospitalLocation>();

    public virtual ICollection<HospitalService> HospitalServices { get; } = new List<HospitalService>();

    public virtual ICollection<HospitalTag> HospitalTags { get; } = new List<HospitalTag>();
}
