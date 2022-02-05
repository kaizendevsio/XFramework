using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class AuthenticateCredentialQuery : QueryBaseEntity, IRequest<QueryResponseBO<AuthorizeIdentityResponse>>
{
    public string Username { get; set; }
    public string Password { get; set; }
    public bool Remember { get; set; }
    public AuthorizeBy AuthorizeBy { get; set; }
}