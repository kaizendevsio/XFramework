using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Delete;

public class DeleteIdentityRequest : RequestBase
{
    public Guid Guid { get; set; }
}