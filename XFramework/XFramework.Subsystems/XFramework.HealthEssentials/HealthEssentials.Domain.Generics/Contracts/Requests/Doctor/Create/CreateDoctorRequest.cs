using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor.Create;

public class CreateDoctorRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public string? ProfessionalName { get; set; }
    public string? PrcNumber { get; set; }
    public string? PtrNumber { get; set; }
    public string? PhilHealthNumber { get; set; }
    public string? TinNumber { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Description { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Clinic { get; set; }
    public string? ClinicAddress { get; set; }
    public decimal? BaseFee { get; set; }

    public CreateAddressRequest? Address { get; set; }
    public List<string>? SupportedConsultationList { get; set; }
    public List<FileUploadRequest>? FileList { get; set; }
}