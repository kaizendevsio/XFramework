namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Create;

public class CreatePharmacyJobOrderMedicineRequest : RequestBase
{
    public Guid? PharmacyJobOrderGuid { get; set; }
    public Guid? MedicineGuid { get; set; }
    public Guid? MedicineIntakeGuid { get; set; }
    public int Quantity { get; set; }
    public string? PrescriptionNote { get; set; }
    public string? Remarks { get; set; }
    public short? Status { get; set; }
}