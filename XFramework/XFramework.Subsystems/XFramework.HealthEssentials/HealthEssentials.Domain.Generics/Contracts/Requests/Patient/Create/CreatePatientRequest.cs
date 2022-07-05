using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Patient.Create;

public class CreatePatientRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public string? Description { get; set; }
    public string? Remarks { get; set; }
    
    public CreateAddressRequest? Address { get; set; }
}