namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Create;

public class CreateMedicineEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}