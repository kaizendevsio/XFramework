using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Unit;

public class GetUnitEntityGroupListQuery : GetUnitEntityGroupListRequest, IRequest<QueryResponse<List<UnitEntityGroupResponse>>>
{
    
}