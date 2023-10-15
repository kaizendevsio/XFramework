namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientReminder : BaseModel
{
    public Guid PatientId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public bool IsSeen { get; set; }

    public short Status { get; set; }


    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}