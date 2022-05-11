namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientAilmentResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientId { get; set; }
    public long AilmentId { get; set; }
    public string? Remarks { get; set; }
    public string Guid { get; set; } = null!;
    
}