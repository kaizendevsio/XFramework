namespace HealthEssentials.Domain.Generics.Contracts;

public partial class DoctorConsultationJobOrder
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid DoctorId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    
    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;
}
