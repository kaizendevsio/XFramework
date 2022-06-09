using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandleListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderHandleListQuery, QueryResponse<List<LogisticRiderHandleResponse>>>
{
    public GetLogisticRiderHandleListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LogisticRiderHandleResponse>>> Handle(GetLogisticRiderHandleListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}