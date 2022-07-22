using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticJobOrderListQuery : GetLogisticJobOrderListRequest, IRequest<QueryResponse<List<LogisticJobOrderResponse>>>
{
    
}