using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderHandleHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderHandleQuery, QueryResponse<LogisticRiderHandleResponse>>
{
    public GetLogisticRiderHandleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticRiderHandleResponse>> Handle(GetLogisticRiderHandleQuery request, CancellationToken cancellationToken)
    {
        var handle = await _dataLayer.HealthEssentialsContext.LogisticRiderHandles
            .Include(x => x.LogisticRider)
            .Include(x => x.Logistic)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (handle is null)
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
            Response = handle.Adapt<LogisticRiderHandleResponse>()
        };
    }
}