namespace HealthEssentials.Domain.Generics.Contracts.Responses.Pharmacy;

public class PharmacyJobOrderMedicineResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PharmacyJobOrderId { get; set; }
    public long? MedicineId { get; set; }
    public long? MedicineIntakeId { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
    public string Guid { get; set; } = null!;

}