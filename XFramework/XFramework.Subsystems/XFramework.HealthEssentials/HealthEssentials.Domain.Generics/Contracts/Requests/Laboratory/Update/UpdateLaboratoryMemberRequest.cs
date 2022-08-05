using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Update;

public class UpdateLaboratoryMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? LaboratoryGuid { get; set; }
    public string? Value { get; set; }
    public string? Name { get; set; }
    public int Status { get; set; }
    public string? Description { get; set; }
    public Guid? LaboratoryLocationGuid { get; set; }
}