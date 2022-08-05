namespace HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Update;

public class UpdateUnitRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? Description { get; set; }
}