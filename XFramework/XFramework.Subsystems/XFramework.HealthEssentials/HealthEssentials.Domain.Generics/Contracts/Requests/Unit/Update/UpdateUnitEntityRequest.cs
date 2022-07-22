namespace HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Update;

public class UpdateUnitEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}