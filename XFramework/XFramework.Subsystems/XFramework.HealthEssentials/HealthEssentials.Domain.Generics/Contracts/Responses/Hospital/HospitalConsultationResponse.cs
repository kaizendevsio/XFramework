namespace HealthEssentials.Domain.Generics.Contracts.Responses.Hospital;

public class HospitalConsultationResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long HospitalId { get; set; }
    public long? ConsultationId { get; set; }
    public string Guid { get; set; } = null!;
}