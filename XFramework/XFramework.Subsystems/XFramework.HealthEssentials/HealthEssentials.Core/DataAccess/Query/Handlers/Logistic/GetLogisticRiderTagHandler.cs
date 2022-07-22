using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderTagHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderTagQuery, QueryResponse<LogisticRiderTagResponse>>
{
    public GetLogisticRiderTagHandler()
    {
        
    }
    public async Task<QueryResponse<LogisticRiderTagResponse>> Handle(GetLogisticRiderTagQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}