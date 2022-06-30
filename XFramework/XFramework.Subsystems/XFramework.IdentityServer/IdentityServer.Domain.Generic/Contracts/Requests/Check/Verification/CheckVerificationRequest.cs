using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Check.Verification;

public class CheckVerificationRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
    public Guid? VerificationTypeGuid { get; set; }
}