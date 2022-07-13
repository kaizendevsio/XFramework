using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

public class CreateConsultationEntityRequest : RequestBase
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? Guid { get; set; }
    public long GroupId { get; set; }
    public int? SortOrder { get; set; }

    public ConsultationEntityGroupResponse? Group { get; set; }
}