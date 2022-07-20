using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandleListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderHandleListQuery, QueryResponse<List<LogisticRiderHandleResponse>>>
{
    public GetLogisticRiderHandleListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LogisticRiderHandleResponse>>> Handle(GetLogisticRiderHandleListQuery request, CancellationToken cancellationToken)
    {
        var logisticRiderHandle = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles
            .Include(i => i.Logistic)
            .ThenInclude(i => i.Entity)
            .Include(i => i.LogisticRider)
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!logisticRiderHandle.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Rider Handle Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Rider Handle Found",
            IsSuccess = true,
            Response = logisticRiderHandle.Adapt<List<LogisticRiderHandleResponse>>()
        };
    }
}