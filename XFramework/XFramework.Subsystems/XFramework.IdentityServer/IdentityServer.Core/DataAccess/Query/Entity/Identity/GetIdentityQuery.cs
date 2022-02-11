using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity;

public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<IdentityResponse>>
{
    public Guid Guid { get; set; }   
}