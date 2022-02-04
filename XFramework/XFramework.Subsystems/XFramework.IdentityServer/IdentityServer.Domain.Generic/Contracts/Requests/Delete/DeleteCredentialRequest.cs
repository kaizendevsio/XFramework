using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Delete;

public class DeleteCredentialRequest : RequestBase
{
    public Guid? Guid { get; set; }
    public string Username { get; set; }
        
}