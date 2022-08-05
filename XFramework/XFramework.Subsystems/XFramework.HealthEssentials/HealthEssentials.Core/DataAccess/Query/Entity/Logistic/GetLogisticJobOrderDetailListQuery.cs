using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticJobOrderDetailListQuery : GetLogisticJobOrderDetailListRequest, IRequest<QueryResponse<List<LogisticJobOrderDetailResponse>>>
{
    
}