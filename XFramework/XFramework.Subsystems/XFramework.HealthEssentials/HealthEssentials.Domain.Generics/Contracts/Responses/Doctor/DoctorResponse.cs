namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;


public class DoctorResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long EntityId { get; set; }
    public long CredentialId { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public string Guid { get; set; } = null!;
    public string Name { get; set; } = null!;
    public int? ExperienceYears { get; set; }
    public string? Clinic { get; set; }
    public string? ClinicAddress { get; set; }
    public decimal? BaseFee { get; set; }
    
    public DoctorEntity Entity { get; set; } = null!;
}