using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticEntityHandler : QueryBaseHandler, IRequestHandler<GetLogisticEntityQuery, QueryResponse<LogisticEntityResponse>>
{
    public GetLogisticEntityHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticEntityResponse>> Handle(GetLogisticEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}