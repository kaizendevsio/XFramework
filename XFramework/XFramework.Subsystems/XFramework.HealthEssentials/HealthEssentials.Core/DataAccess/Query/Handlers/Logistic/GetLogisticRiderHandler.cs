using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderQuery, QueryResponse<LogisticRiderResponse>>
{
    public GetLogisticRiderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticRiderResponse>> Handle(GetLogisticRiderQuery request, CancellationToken cancellationToken)
    {
        var rider = await _dataLayer.HealthEssentialsContext.LogisticRiders
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (rider is null)
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
            Response = rider.Adapt<LogisticRiderResponse>()
        };
    }
}