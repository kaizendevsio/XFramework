using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderQuery, QueryResponse<LogisticJobOrderResponse>>
{
    public GetLogisticJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticJobOrderResponse>> Handle(GetLogisticJobOrderQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders
            .Include(x => x.Rider)
            .Include(x => x.Schedule)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (jobOrder is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = jobOrder.Adapt<LogisticJobOrderResponse>()
        };
    }
}