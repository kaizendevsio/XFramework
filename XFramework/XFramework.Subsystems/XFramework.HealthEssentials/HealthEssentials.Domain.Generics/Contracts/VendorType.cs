namespace HealthEssentials.Domain.Generics.Contracts;

public partial class VendorType
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public string Name { get; set; } = null!;

    
    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual VendorTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Vendor> Vendors { get; } = new List<Vendor>();
}
