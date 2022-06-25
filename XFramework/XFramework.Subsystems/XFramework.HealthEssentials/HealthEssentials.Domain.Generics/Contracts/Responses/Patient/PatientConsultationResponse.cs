

using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientConsultationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Guid { get; set; } = null!;

    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
    public PatientResponse? Patient { get; set; }
}