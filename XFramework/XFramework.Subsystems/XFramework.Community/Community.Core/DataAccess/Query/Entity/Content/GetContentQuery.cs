using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Responses.Content;

namespace Community.Core.DataAccess.Query.Entity.Content;

public class GetContentQuery : GetContentRequest, IRequest<QueryResponse<CommunityContentResponse>>
{
    
}