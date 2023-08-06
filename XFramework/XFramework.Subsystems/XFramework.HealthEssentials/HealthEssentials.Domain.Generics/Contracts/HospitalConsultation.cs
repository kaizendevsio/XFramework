namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalConsultation
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid HospitalId { get; set; }

    public Guid ConsultationId { get; set; }

    
    public virtual Consultation? Consultation { get; set; }

    public virtual Hospital Hospital { get; set; } = null!;
}
