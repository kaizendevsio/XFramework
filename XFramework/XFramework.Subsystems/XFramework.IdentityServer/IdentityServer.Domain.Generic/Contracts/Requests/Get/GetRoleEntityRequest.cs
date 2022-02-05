using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Get;

public class GetRoleEntityRequest : RequestBase
{
    public Guid? Guid { get; set; }
}