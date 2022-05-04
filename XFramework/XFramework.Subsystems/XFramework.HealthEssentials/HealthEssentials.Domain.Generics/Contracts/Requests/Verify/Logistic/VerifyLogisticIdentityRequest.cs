using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Logistic;

public class VerifyLogisticIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}