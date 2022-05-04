using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Verify.Pharmacy;

public class VerifyPharmacyIdentityRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}