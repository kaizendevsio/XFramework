namespace HealthEssentials.Domain.Generics.Contracts;

public partial class MedicineVendor : BaseModel
{
    public Guid MedicineId { get; set; }

    public Guid VendorId { get; set; }


    public virtual Medicine Medicine { get; set; } = null!;

    public virtual Vendor Vendor { get; set; } = null!;
}