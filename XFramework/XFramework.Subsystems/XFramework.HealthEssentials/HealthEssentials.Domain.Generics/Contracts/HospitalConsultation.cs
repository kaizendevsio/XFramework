namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalConsultation : BaseModel
{
    public Guid HospitalId { get; set; }

    public Guid ConsultationId { get; set; }


    public virtual Consultation? Consultation { get; set; }

    public virtual Hospital Hospital { get; set; } = null!;
}