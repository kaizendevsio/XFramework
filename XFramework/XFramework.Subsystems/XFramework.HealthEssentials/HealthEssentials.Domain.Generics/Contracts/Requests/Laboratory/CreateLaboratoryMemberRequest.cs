using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class CreateLaboratoryMemberRequest : RequestBase
{
    public Guid? LaboratoryGuid { get; set; }
    public Guid? CredentialGuid { get; set; }
    public string? ProfessionalName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Description { get; set; }
    
    public CreateAddressRequest? Address { get; set; }
}