namespace HealthEssentials.Domain.Generics.Contracts;

public partial class LaboratoryServiceTypeGroup
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public virtual ICollection<LaboratoryServiceType> LaboratoryServiceTypes { get; } = new List<LaboratoryServiceType>();
}
