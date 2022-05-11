using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Doctor;

public class CreateDoctorIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public string? ProfessionalName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Description { get; set; }
    public string? Specialty { get; set; }
    public int? ExperienceYears { get; set; }
    public string? Clinic { get; set; }
    public string? ClinicAddress { get; set; }
    public decimal? BaseFee { get; set; }

    public CreateAddressRequest? Address { get; set; }
}