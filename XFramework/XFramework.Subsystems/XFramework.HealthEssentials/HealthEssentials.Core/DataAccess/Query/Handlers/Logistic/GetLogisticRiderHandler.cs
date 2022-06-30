using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderQuery, QueryResponse<LogisticRiderResponse>>
{
    public GetLogisticRiderHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticRiderResponse>> Handle(GetLogisticRiderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}