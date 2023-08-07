namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Doctor
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? Description { get; set; }

    public string? Remarks { get; set; }

    
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

    public virtual DoctorType Type { get; set; } = null!;
}
