namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineIntakeEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
}