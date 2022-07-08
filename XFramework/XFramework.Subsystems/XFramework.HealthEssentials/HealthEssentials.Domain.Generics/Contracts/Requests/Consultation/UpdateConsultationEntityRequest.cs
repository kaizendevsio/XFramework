using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class UpdateConsultationEntityRequest : RequestBase
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? GroupGuid { get; set; }
    public int? SortOrder { get; set; }

}