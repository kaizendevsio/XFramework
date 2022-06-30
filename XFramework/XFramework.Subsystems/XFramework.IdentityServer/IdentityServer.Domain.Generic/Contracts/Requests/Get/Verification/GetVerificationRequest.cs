using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Verification;

public class GetVerificationRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? VerificationTypeGuid { get; set; }
}