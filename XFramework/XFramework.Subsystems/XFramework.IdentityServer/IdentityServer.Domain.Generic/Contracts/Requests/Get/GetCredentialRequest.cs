using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetCredentialRequest : RequestBase
{
    public Guid? Guid { get; set; }
}