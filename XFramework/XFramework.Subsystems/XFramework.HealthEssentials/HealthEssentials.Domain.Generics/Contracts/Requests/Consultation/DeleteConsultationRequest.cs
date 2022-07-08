namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class DeleteConsultationRequest : RequestBase
{
    public Guid? Guid { get; set; }
}