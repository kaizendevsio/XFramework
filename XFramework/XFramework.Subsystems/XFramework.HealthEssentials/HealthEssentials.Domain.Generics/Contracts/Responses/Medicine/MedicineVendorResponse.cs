namespace HealthEssentials.Domain.Generics.Contracts.Responses.Medicine;

public class MedicineVendorResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long MedicineId { get; set; }
    public long VendorId { get; set; }
    public string Guid { get; set; } = null!;
}