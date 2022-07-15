namespace HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;

public class CreateMetaDataEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}