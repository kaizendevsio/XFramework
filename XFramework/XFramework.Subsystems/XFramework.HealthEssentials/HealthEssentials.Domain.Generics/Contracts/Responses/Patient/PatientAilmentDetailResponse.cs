

using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientAilmentDetailResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientAilmentId { get; set; }
    public long? ConsultationJobOrderId { get; set; }
    public string? DoctorName { get; set; }
    public short? Status { get; set; }
    public string? Remarks { get; set; }
    public string Guid { get; set; } = null!;
    
    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
}