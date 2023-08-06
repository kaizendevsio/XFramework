namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalLaboratory
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid HospitalId { get; set; }

    public Guid LaboratoryId { get; set; }

    
    public virtual Hospital Hospital { get; set; } = null!;

    public virtual Laboratory? Laboratory { get; set; }
}
