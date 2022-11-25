using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticJobOrderLocationHandler : QueryBaseHandler, IRequestHandler<GetLogisticJobOrderLocationQuery, QueryResponse<LogisticJobOrderLocationResponse>>
{
    public GetLogisticJobOrderLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticJobOrderLocationResponse>> Handle(GetLogisticJobOrderLocationQuery request, CancellationToken cancellationToken)
    {
        var location = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations
            .Include(x => x.LogisticJobOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (location is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No record found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Record found",
            Response = location.Adapt<LogisticJobOrderLocationResponse>()
        };
    }
}