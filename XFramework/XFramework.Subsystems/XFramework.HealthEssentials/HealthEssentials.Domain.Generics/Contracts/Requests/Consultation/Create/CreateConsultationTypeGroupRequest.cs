using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation.Create;

public class CreateConsultationTypeGroupRequest : RequestBase
{
    public string? Name { get; set; }
}