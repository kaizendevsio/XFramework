namespace HealthEssentials.Domain.Generics.Contracts;

public partial class PharmacyService
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid PharmacyLocationId { get; set; }

    public Guid PharmacyId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public decimal? Quantity { get; set; }

    public Guid UnitId { get; set; }

    
    public virtual PharmacyServiceType Type { get; set; } = null!;

    public virtual Pharmacy Pharmacy { get; set; } = null!;

    public virtual PharmacyLocation PharmacyLocation { get; set; } = null!;

    public virtual ICollection<PharmacyServiceTag> PharmacyServiceTags { get; } = new List<PharmacyServiceTag>();

    public virtual Unit? Unit { get; set; }
}
