using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace HealthEssentials.Domain.Generics.Contracts.Responses.Doctor;

public class DoctorResponse
{
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public long CredentialId { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public Guid? Guid { get; set; }
    public string? Name { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Clinic { get; set; }
    public string? ClinicAddress { get; set; }
    public decimal? BaseFee { get; set; }
    public string? PrcNumber { get; set; }
    public string? PtrNumber { get; set; }
    public string? PhilHealthNumber { get; set; }
    public string? TinNumber { get; set; }
    public int Status { get; set; }
    
    public DoctorEntityResponse? Entity { get; set; }
    public List<DoctorTagResponse>? DoctorTags { get; set; }
    public List<DoctorConsultationResponse>? DoctorConsultations { get; set; }
    public CredentialResponse? Credential { get; set; }
    public List<StorageFileResponse>? Files { get; set; }
}