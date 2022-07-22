using HealthEssentials.Domain.Generics.Contracts.Requests.Logistic.Get;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

public class GetLogisticEntityListQuery : GetLogisticEntityListRequest, IRequest<QueryResponse<List<LogisticEntityResponse>>>
{
    
}