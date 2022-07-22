using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderDetailListHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderDetailListQuery, QueryResponse<List<LogisticJobOrderDetailResponse>>>
{
    public GetLogisticJobOrderDetailListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LogisticJobOrderDetailResponse>>> Handle(GetLogisticJobOrderDetailListQuery request, CancellationToken cancellationToken)
    {
        var jobOrderDetail = await _dataLayer.HealthEssentialsContext.LogisticJobOrderDetails
            .Include(x => x.LogisticJobOrder)
            .Include(x => x.Unit)
            .Where(x => EF.Functions.Like(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!jobOrderDetail.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Job Order Detail found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Job Order Detail found",
            IsSuccess = true,
            Response = jobOrderDetail.Adapt<List<LogisticJobOrderDetailResponse>>()
        };
    }
}