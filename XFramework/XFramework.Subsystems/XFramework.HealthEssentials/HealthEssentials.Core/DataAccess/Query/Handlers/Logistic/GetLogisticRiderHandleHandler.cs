using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandleHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderHandleQuery, QueryResponse<LogisticRiderHandleResponse>>
{
    public GetLogisticRiderHandleHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticRiderHandleResponse>> Handle(GetLogisticRiderHandleQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}