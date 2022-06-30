using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Responses.Connection;

namespace Community.Core.DataAccess.Query.Entity.Connection;

public class GetConnectionListQuery : GetConnectionListRequest, IRequest<QueryResponse<List<CommunityConnectionResponse>>>
{
    
}