using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Application;

public class GetAppAppListQuery : QueryBaseEntity, IRequest<QueryResponseBO<List<GetApplicationListResponse>>>
{
        
}