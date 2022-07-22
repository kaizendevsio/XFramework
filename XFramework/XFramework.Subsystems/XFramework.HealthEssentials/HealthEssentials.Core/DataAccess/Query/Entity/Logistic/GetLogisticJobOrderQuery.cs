using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticJobOrderQuery : GetLogisticJobOrderRequest, IRequest<QueryResponse<LogisticJobOrderResponse>>
{
    
}