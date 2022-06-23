using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticHandler : QueryBaseHandler, IRequestHandler<GetLogisticQuery, QueryResponse<LogisticResponse>>
{
    public GetLogisticHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticResponse>> Handle(GetLogisticQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}