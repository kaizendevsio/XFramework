using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandleListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderHandleListQuery, QueryResponse<List<LogisticRiderHandleResponse>>>
{
    public GetLogisticRiderHandleListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LogisticRiderHandleResponse>>> Handle(GetLogisticRiderHandleListQuery request, CancellationToken cancellationToken)
    {
        var logisticRiderHandle = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles
            .AsNoTracking()
            .OrderBy(i => i.CreatedAt)
            .Take(request.PageSize)
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