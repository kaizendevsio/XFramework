using HealthEssentials.Core.DataAccess.Query.Handlers;
using HealthEssentials.Domain.Generics.Contracts.Requests.Laboratory.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

public class GetLaboratoryJobOrderListQuery : GetLaboratoryJobOrderListRequest, IRequest<QueryResponse<List<LaboratoryJobOrderResponse>>>
{
    
}