using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class AuthenticateCredentialQuery : AuthenticateCredentialRequest, IRequest<QueryResponseBO<AuthorizeIdentityResponse>>
{
  
}