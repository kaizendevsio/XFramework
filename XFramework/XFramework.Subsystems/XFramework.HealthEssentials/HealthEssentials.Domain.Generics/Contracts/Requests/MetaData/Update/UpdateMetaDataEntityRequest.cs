namespace HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;

public class UpdateMetaDataEntityRequest : RequestBase
{
    public string? Name { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }
}