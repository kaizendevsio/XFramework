using HealthEssentials.Domain.Generics.Contracts.Responses.Consultation;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalConsultationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string Guid { get; set; } = null!;

    public HospitalResponse? Hospital { get; set; }
    public ConsultationResponse? Consultation { get; set; }
}