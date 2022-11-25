using HealthEssentials.Domain.Generics.Contracts.Requests.Unit.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Unit;

public class GetUnitEntityListQuery : GetUnitEntityListRequest, IRequest<QueryResponse<List<UnitEntityResponse>>>
{
    
}