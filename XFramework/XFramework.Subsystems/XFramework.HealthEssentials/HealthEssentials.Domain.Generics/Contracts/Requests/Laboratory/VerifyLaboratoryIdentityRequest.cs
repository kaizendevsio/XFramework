using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory;

public class VerifyLaboratoryIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}