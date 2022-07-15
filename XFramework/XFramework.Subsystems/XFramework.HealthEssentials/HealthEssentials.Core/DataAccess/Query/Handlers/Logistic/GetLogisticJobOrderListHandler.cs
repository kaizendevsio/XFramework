using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderListHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderListQuery, QueryResponse<List<LogisticJobOrderResponse>>>
{
    public GetLogisticJobOrderListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LogisticJobOrderResponse>>> Handle(GetLogisticJobOrderListQuery request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders
            .Include(x => x.Rider)
            .Include(x => x.Schedule)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!jobOrder.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Job Order found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Job Order found",
            IsSuccess = true,
            Response = jobOrder.Adapt<List<LogisticJobOrderResponse>>()
        };
    }
}