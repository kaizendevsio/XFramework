using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderTagListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderTagListQuery, QueryResponse<List<LogisticRiderTagResponse>>>
{
    public GetLogisticRiderTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LogisticRiderTagResponse>>> Handle(GetLogisticRiderTagListQuery request, CancellationToken cancellationToken)
    {
        var riderTag = await _dataLayer.HealthEssentialsContext.LogisticRiderTags
            .Include(x => x.LogisticRider)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!riderTag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic rider tag found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic rider tag found",
            IsSuccess = true,
            Response = riderTag.Adapt<List<LogisticRiderTagResponse>>()
        };
    }
}