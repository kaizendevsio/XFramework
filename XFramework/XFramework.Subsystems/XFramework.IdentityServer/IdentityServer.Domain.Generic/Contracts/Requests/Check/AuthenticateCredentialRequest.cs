using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Domain.Generic.Contracts.Requests.Check;

public class AuthenticateCredentialRequest : RequestBase
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool Remember { get; set; }
    public AuthorizeBy AuthorizeBy { get; set; }
}