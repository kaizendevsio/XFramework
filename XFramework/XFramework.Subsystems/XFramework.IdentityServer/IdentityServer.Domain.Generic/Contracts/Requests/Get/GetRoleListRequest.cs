using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetRoleListRequest : RequestBase
{
    public Guid? CredentialGuid { get; set; }
}