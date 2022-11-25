using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Ailment.Update;

public class UpdateAilmentTagRequest : RequestBase
{
    public Guid? AilmentGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}