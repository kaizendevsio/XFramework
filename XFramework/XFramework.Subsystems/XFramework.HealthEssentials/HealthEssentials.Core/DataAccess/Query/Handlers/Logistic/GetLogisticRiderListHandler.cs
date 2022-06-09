using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderListQuery, QueryResponse<List<LogisticRiderResponse>>>
{
    public GetLogisticRiderListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LogisticRiderResponse>>> Handle(GetLogisticRiderListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}