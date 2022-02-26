using IdentityServer.Domain.Generic.Contracts.Requests.Get;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class GetCredentialQuery : GetCredentialRequest, IRequest<QueryResponseBO<CredentialResponse>>
{
    
}