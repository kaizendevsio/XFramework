using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Ailment;

public class UpdateAilmentRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public string? Name { get; set; }
    public string? ShortName { get; set; }
    public string? OtherName { get; set; }
    public string? Description { get; set; }
}