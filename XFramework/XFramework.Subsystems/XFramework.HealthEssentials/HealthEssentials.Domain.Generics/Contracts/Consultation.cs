namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Consultation : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }


    public virtual ICollection<ConsultationJobOrder> ConsultationJobOrders { get; set; } = new List<ConsultationJobOrder>();

    public virtual ICollection<ConsultationTag> ConsultationTags { get; set; } = new List<ConsultationTag>();

    public virtual ICollection<DoctorConsultation> DoctorConsultations { get; set; } = new List<DoctorConsultation>();

    public virtual ConsultationType Type { get; set; } = null!;

    public virtual ICollection<HospitalConsultation> HospitalConsultations { get; set; } = new List<HospitalConsultation>();
}