namespace HealthEssentials.Domain.Generics.Contracts;

public partial class HospitalService
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid HospitalLocationId { get; set; }

    public Guid HospitalId { get; set; }

    public string? Name { get; set; }

    public string? ShortName { get; set; }

    public string? Description { get; set; }

    public decimal? Price { get; set; }

    public decimal? MaxDiscount { get; set; }

    public decimal? Quantity { get; set; }

    public Guid UnitId { get; set; }

    
    public virtual HospitalServiceType Type { get; set; } = null!;

    public virtual Hospital Hospital { get; set; } = null!;

    public virtual HospitalLocation HospitalLocation { get; set; } = null!;

    public virtual ICollection<HospitalServiceTag> HospitalServiceTags { get; } = new List<HospitalServiceTag>();

    public virtual Unit? Unit { get; set; }
}
