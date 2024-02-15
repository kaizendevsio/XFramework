namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Vendor : BaseModel
{
    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool? IsGenericProvider { get; set; }


    public virtual VendorType Type { get; set; } = null!;

    public virtual ICollection<MedicineVendor> MedicineVendors { get; set; } = new List<MedicineVendor>();
}