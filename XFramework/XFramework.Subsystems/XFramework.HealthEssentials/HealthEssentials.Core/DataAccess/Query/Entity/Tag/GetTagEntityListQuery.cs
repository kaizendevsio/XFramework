using HealthEssentials.Domain.Generics.Contracts.Requests.Tag.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Tag;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Tag;

public class GetTagEntityListQuery : GetTagEntityListRequest, IRequest<QueryResponse<List<TagEntityResponse>>>
{
    
}