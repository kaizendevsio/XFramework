namespace HealthEssentials.Domain.Generics.Contracts;

public partial class Vendor
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public bool? IsGenericProvider { get; set; }

    
    public virtual VendorType Type { get; set; } = null!;

    public virtual ICollection<MedicineVendor> MedicineVendors { get; } = new List<MedicineVendor>();
}
