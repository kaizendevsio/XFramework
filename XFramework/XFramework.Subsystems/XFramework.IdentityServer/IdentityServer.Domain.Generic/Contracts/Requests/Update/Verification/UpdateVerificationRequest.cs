using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Update.Verification;

public class UpdateVerificationRequest : RequestBase
{
    public GenericStatusType Status { get; set; }
    public Guid? Guid { get; set; }
}