using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Update;

public class UpdateDoctorRequest : RequestBase
{
    public Guid? EntityGuid { get; set; }
    public Guid? CredentialGuid { get; set; }
    
    public bool? IsEnabled { get; set; }
    public bool IsDeleted { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    public string? Name { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Clinic { get; set; }
    public string? ClinicAddress { get; set; }
    public decimal? BaseFee { get; set; }
    public string? PrcNumber { get; set; }
    public string? PtrNumber { get; set; }
    public string? PhilHealthNumber { get; set; }
    public string? TinNumber { get; set; }
    public GenericStatusType Status { get; set; }
}