using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetIdentityRoleRequest : RequestBase
{
    public Guid? Guid { get; set; }
}