using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Verify;

public class VerifyLogisticRiderRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}