namespace HealthEssentials.Domain.Generics.Contracts;

public partial class VendorType : BaseModel
{
    public string Name { get; set; } = null!;


    public Guid GroupId { get; set; }

    public int? SortOrder { get; set; }

    public virtual VendorTypeGroup Group { get; set; } = null!;

    public virtual ICollection<Vendor> Vendors { get; } = new List<Vendor>();
}