namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}