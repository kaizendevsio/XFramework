namespace HealthEssentials.Domain.Generics.Contracts.Requests.MetaData.Create;

public class CreateMetaDatumRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public Guid? KeyGuid { get; set; }
    public string? Value { get; set; }
}