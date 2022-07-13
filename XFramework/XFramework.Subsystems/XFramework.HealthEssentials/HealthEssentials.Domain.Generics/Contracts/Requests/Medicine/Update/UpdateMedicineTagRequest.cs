namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

public class UpdateMedicineTagRequest : RequestBase
{
    public Guid MedicineGuid { get; set; }
    public string? Value { get; set; }
    public Guid TagGuid { get; set; }
}