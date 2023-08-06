namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientReminder
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PatientId { get; set; }

    public Guid ConsultationJobOrderId { get; set; }

    public bool IsSeen { get; set; }

    public short Status { get; set; }

    
    public virtual ConsultationJobOrder? ConsultationJobOrder { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}
