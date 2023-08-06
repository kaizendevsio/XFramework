namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceTypeGroup
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public virtual ICollection<HospitalServiceType> HospitalServiceTypes { get; } = new List<HospitalServiceType>();
}
