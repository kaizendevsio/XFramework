namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineVendorRequest : RequestBase
{
    public Guid? MedicineGuid { get; set; }
    public Guid? VendorGuid { get; set; }
}