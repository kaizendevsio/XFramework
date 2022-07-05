using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticRiderHandleListQuery : GetLogisticRiderHandleListRequest, IRequest<QueryResponse<List<LogisticRiderHandleResponse>>>
{
    
}