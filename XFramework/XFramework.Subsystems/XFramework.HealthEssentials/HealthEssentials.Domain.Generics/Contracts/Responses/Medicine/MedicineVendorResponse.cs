namespace HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

public class MedicineVendorResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }

    public MedicineResponse? Medicine { get; set; }
    public MedicineVendorResponse? Vendor { get; set; }
}