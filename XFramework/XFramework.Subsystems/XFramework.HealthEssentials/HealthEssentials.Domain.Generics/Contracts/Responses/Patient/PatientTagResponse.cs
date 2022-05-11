namespace HealthEssentials.Domain.Generics.Contracts.Responses.Patient;

public class PatientTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long PatientId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public string Guid { get; set; } = null!;

}