namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class DeleteConsultationEntityRequest : RequestBase
{
    public Guid? Guid { get; set; }
}