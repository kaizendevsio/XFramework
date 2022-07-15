using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderQuery, QueryResponse<LogisticJobOrderResponse>>
{
    public GetLogisticJobOrderHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticJobOrderResponse>> Handle(GetLogisticJobOrderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}