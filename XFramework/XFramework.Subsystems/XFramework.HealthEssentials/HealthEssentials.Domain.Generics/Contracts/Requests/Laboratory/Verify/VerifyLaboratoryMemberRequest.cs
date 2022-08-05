using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Verify;

public class VerifyLaboratoryMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}