namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientConsultation : BaseModel
{
    public Guid PatientId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }


    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}