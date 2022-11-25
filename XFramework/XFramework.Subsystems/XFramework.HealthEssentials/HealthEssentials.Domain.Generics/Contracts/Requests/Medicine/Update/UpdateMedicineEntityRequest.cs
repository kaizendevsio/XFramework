namespace HealthEssentials.Domain.Generics.Contracts.Requests.Medicine.Update;

public class UpdateMedicineEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}