

using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorConsultationJobOrderResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }
    
    public ConsultationJobOrderResponse? ConsultationJobOrder { get; set; }
    public DoctorResponse? Doctor { get; set; }
}