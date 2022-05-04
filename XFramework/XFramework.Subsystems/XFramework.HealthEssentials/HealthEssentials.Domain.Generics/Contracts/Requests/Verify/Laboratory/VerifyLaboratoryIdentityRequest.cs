using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Laboratory;

public class VerifyLaboratoryIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}