using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Create;

public class CreateAilmentTagRequest : RequestBase
{
    public Guid? AilmentGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}