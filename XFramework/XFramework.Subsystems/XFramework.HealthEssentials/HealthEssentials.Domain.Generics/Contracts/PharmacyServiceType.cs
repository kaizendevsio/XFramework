namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyServiceType : BaseModel
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual PharmacyServiceTypeGroup Group { get; set; } = null!;

    public virtual ICollection<PharmacyService> PharmacyServices { get; } = new List<PharmacyService>();
}