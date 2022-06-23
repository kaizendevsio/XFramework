using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class VerifyLaboratoryMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}