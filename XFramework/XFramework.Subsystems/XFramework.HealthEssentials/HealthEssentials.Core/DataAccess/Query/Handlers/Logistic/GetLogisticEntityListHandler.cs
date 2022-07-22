using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticEntityListHandler : QueryBaseHandler, IRequestHandler<GetLogisticEntityListQuery, QueryResponse<List<LogisticEntityResponse>>>
{
    public GetLogisticEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LogisticEntityResponse>>> Handle(GetLogisticEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.LogisticEntities
            .Where(x => EF.Functions.Like(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!entity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Entities found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Entities found",
            IsSuccess = true,
            Response = entity.Adapt<List<LogisticEntityResponse>>()
        };
    }
}