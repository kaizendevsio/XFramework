using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticListHandler : QueryBaseHandler, IRequestHandler<GetLogisticListQuery, QueryResponse<List<LogisticResponse>>>
{
    public GetLogisticListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LogisticResponse>>> Handle(GetLogisticListQuery request, CancellationToken cancellationToken)
    {
        var logistic = await _dataLayer.HealthEssentialsContext.Logistics
            .Include(i => i.Entity)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.Status)
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!logistic.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Logistic Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Logistic Found",
            IsSuccess = true,
            Response = logistic.Adapt<List<LogisticResponse>>()
        };
    }
}