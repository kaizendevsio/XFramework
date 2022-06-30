using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Create.Verification;

public class CreateVerificationRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? VerificationTypeGuid { get; set; }
}