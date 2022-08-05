using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderLocationHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderLocationQuery, QueryResponse<LogisticJobOrderLocationResponse>>
{
    public GetLogisticJobOrderLocationHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticJobOrderLocationResponse>> Handle(GetLogisticJobOrderLocationQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}