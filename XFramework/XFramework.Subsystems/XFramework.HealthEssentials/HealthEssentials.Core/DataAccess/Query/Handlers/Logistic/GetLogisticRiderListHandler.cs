using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderListHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderListQuery, QueryResponse<List<LogisticRiderResponse>>>
{
    public GetLogisticRiderListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LogisticRiderResponse>>> Handle(GetLogisticRiderListQuery request, CancellationToken cancellationToken)
    {
        var logisticRider = await _dataLayer.HealthEssentialsContext.LogisticRiders
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        if (!logisticRider.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Rider Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Rider Found",
            IsSuccess = true,
            Response = logisticRider.Adapt<List<LogisticRiderResponse>>()
        };
    }
}