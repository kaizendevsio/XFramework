namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PatientLaboratory
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid PatientId { get; set; }

    public Guid LaboratoryJobOrderId { get; set; }

    
    public virtual LaboratoryJobOrder? LaboratoryJobOrder { get; set; }

    public virtual Patient Patient { get; set; } = null!;
}
