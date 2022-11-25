namespace HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Update;

public class UpdateMetaDatumRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public Guid? KeyGuid { get; set; }
    public string? Value { get; set; }
}