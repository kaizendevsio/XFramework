using XFramework.Domain.Generic.Contracts.Requests;

namespace HealthEssentials.Domain.Generics.Contracts.Requests.Pharmacy.Verify;

public class VerifyPharmacyMemberRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}