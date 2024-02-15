namespace HealthEssentials.Domain.Generics.Contracts;

public partial class VendorTypeGroup : BaseModel
{
    public string Name { get; set; } = null!;


    public virtual ICollection<VendorType> VendorTypes { get; set; } = new List<VendorType>();
}