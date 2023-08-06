namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalServiceType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    
    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual HospitalServiceTypeGroup Group { get; set; } = null!;

    public virtual ICollection<HospitalService> HospitalServices { get; } = new List<HospitalService>();
}
