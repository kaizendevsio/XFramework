using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class UpdateLaboratoryMemberRequest : RequestBase
{
    public Guid? LaboratoryGuid { get; set; }
    public Guid? CredentialGuid { get; set; }
    public string? ProfessionalName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Description { get; set; }
}