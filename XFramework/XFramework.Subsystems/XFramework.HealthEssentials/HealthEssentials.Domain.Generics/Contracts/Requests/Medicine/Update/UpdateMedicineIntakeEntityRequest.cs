namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

public class UpdateMedicineIntakeEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public int? SortOrder { get; set; }
}