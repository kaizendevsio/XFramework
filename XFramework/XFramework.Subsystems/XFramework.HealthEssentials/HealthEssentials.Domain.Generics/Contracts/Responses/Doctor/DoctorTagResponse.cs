namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorTagResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long DoctorId { get; set; }
    public string? Value { get; set; }
    public long? TagId { get; set; }
    public string Guid { get; set; } = null!;
    
    /*public Doctor Doctor { get; set; } = null!;*/
}