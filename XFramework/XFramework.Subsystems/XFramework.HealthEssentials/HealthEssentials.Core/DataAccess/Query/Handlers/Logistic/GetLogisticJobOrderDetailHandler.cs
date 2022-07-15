using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderDetailHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderDetailQuery, QueryResponse<LogisticJobOrderDetailResponse>>
{
    public GetLogisticJobOrderDetailHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticJobOrderDetailResponse>> Handle(GetLogisticJobOrderDetailQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}