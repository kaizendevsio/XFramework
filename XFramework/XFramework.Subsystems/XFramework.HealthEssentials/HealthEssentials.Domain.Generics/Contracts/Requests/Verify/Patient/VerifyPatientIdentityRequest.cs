using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Patient;

public class VerifyPatientIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}