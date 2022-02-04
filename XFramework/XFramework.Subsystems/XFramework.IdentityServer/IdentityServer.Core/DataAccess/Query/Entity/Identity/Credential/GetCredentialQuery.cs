using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;

public class GetCredentialQuery : QueryBaseEntity, IRequest<QueryResponseBO<IdentityCredentialResponse>>
{
    public Guid? Guid { get; set; }   
}