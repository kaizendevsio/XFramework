using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryMemberRequest : RequestBase
{
    public Guid? LaboratoryGuid { get; set; }
    public Guid? LaboratoryLocationGuid { get; set; }
    public Guid? CredentialGuid { get; set; }
    
    public string? Value { get; set; }
    public string? Name { get; set; }
    public GenericStatusType Status { get; set; }
}