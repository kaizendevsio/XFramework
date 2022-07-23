namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyJobOrderMedicineRequest : RequestBase
{
    public Guid? PharmacyJobOrderGuid { get; set; }
    public Guid? MedicineId { get; set; }
    public Guid? MedicineIntakeGuid { get; set; }
    public string? Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}