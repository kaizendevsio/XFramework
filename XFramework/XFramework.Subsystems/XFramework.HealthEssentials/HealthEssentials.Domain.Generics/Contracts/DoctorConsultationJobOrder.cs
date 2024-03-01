namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorConsultationJobOrder : BaseModel
{
    public Guid DoctorId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }


    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;
}