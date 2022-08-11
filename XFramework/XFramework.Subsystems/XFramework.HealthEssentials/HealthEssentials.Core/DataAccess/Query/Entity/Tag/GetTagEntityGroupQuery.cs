using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Tag;

public class GetTagEntityGroupQuery : GetTagEntityGroupRequest, IRequest<QueryResponse<TagEntityGroupResponse>>
{
    
}