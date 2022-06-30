using XFramework.Domain.Generic.Contracts.Requests;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Update;

public class UpdateCredentialRequest : TransactionRequestBase
{
    public Guid? IdentityGuid { get; set; }
    public Guid? Guid { get; set; }
    public string UserAlias { get; set; }
    public string UserName { get; set; }
    public string Token { get; set; }
}