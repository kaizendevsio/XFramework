namespace HealthEssentials.Domain.Generics.Contracts.Requests.Consultation;

public class CreateConsultationTagRequest : RequestBase
{
    public Guid? ConsultationGuid { get; set; }
    public string? Value { get; set; }
    public Guid? TagGuid { get; set; }
}