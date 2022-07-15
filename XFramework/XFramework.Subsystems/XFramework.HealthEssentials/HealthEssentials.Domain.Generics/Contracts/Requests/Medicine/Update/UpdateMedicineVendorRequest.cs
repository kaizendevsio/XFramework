namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

public class UpdateMedicineVendorRequest : RequestBase
{
    public Guid? MedicineGuid { get; set; }
    public Guid? VendorGuid { get; set; }
}