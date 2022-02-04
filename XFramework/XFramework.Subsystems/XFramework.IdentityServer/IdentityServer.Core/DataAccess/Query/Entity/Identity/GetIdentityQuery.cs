using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity;

public class GetIdentityQuery : QueryBaseEntity, IRequest<QueryResponseBO<IdentityInfoResponse>>
{
    public Guid Guid { get; set; }   
}