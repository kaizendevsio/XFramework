namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineTagRequest : RequestBase
{
    public Guid? MedicineGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}