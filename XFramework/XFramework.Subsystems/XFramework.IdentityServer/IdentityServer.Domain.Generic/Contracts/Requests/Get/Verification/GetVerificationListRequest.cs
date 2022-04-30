using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get.Verification;

public class GetVerificationListRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}