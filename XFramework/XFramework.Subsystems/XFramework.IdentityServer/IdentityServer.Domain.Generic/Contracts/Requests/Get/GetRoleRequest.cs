using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetRoleRequest : RequestBase
{
    public Guid? Guid { get; set; }
}