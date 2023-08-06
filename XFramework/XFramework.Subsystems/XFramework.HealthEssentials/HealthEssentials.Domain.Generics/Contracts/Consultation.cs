namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Consultation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    
    public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; } = new List<ConsultationJobOrder>();

    public virtual ICollection<ConsultationTag> ConsultationTags { get; } = new List<ConsultationTag>();

    public virtual ICollection<DoctorConsultation> DoctorConsultations { get; } = new List<DoctorConsultation>();

    public virtual ConsultationType Type { get; set; } = null!;

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; } = new List<HospitalConsultation>();
}
