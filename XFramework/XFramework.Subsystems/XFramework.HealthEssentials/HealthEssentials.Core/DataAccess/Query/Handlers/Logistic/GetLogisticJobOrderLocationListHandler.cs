using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderLocationListHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderLocationListQuery, QueryResponse<List<LogisticJobOrderLocationResponse>>>
{
    public GetLogisticJobOrderLocationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LogisticJobOrderLocationResponse>>> Handle(GetLogisticJobOrderLocationListQuery request, CancellationToken cancellationToken)
    {
        var location = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations
            .Include(x => x.LogisticJobOrder)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!location.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Job Order Location found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Job Order Location found",
            IsSuccess = true,
            Response = location.Adapt<List<LogisticJobOrderLocationResponse>>()
        };
    }
}