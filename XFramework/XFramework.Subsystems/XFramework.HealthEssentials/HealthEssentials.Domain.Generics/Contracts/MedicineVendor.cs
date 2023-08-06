namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineVendor
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid MedicineId { get; set; }

    public Guid VendorId { get; set; }

    
    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}
