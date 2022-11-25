using System;
using System.Collections.Generic;

namespace HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

public partial class Doctor
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public long EntityId { get; set; }

    public string CredentialGuid { get; set; } = null!;

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    public string Guid { get; set; } = null!;

    public string Name { get; set; } = null!;

    public int? ExperienceYears { get; set; }

    public string? Clinic { get; set; }

    public string? ClinicAddress { get; set; }

    public decimal? BaseFee { get; set; }

    public string? PrcNumber { get; set; }

    public string? PtrNumber { get; set; }

    public string? PhilHealthNumber { get; set; }

    public string? TinNumber { get; set; }

    public int Status { get; set; }

    public virtual ICollection<DoctorConsultationJobOrder> DoctorConsultationJobOrders { get; } = new List<DoctorConsultationJobOrder>();

    public virtual ICollection<DoctorConsultation> DoctorConsultations { get; } = new List<DoctorConsultation>();

    public virtual ICollection<DoctorTag> DoctorTags { get; } = new List<DoctorTag>();

    public virtual DoctorEntity Entity { get; set; } = null!;
}
