using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class GetCredentialQuery : QueryBaseEntity, IRequest<QueryResponseBO<CredentialResponse>>
{
    public Guid? Guid { get; set; }   
}