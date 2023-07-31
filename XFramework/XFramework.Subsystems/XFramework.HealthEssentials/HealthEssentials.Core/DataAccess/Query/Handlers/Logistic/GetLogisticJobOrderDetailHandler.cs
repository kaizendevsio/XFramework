using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderDetailHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderDetailQuery, QueryResponse<LogisticJobOrderDetailResponse>>
{
    public GetLogisticJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticJobOrderDetailResponse>> Handle(GetLogisticJobOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await _dataLayer.HealthEssentialsContext.LogisticJobOrderDetails
            .Include(x => x.LogisticJobOrder)
            .Include(x => x.Unit)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (detail is null)
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
            Response = detail.Adapt<LogisticJobOrderDetailResponse>()
        };
    }
}