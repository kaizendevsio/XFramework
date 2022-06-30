using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorConsultationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? Guid { get; set; }
    public decimal? Price { get; set; }
    public decimal? MaxDiscount { get; set; }
    public int Quantity { get; set; }
    
    public ConsultationResponse? Consultation { get; set; }
    public DoctorResponse? Doctor { get; set; }
}