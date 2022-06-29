using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class CreateConsultationEntityRequest : RequestBase
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public Guid? Guid { get; set; }
    public long GroupId { get; set; }
    public int? SortOrder { get; set; }

    public ConsultationEntityGroup Group { get; set; } = null!;
}