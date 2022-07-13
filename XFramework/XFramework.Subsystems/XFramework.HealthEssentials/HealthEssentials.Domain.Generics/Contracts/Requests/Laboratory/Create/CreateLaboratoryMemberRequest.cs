using IdentityServer.Domain.Generic.Contracts.Requests.Create.Address;
using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Create;

public class CreateLaboratoryMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? LaboratoryGuid { get; set; }
    public Guid? LaboratoryLocationGuid { get; set; }
    
    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? Description { get; set; }
    public GenericStatusType Status { get; set; }
}