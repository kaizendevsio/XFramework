using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Update;

public class UpdateConsultationEntityRequest : RequestBase
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public long GroupGuid { get; set; }
    public int? SortOrder { get; set; }

    public ConsultationEntityGroupResponse? Group { get; set; }
}