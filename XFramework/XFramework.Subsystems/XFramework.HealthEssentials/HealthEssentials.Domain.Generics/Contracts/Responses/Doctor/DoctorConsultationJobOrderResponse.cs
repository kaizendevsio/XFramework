

using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorConsultationJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long DoctorId { get; set; }
    public long? ConsultationJobOrderId { get; set; }
    public string Guid { get; set; } = null!;
    
    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
}