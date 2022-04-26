using Community.Domain.Generic.Contracts.Requests.Get;
using Community.Domain.Generic.Contracts.Responses.Identity;

namespace Community.Core.DataAccess.Query.Entity.Identity;

public class GetIdentityQuery : GetIdentityRequest, IRequest<QueryResponse<CommunityIdentityResponse>>
{
    
}