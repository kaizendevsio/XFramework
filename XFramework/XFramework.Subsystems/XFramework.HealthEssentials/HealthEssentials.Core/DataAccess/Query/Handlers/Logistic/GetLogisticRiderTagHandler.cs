using HealthEssentials.Core.DataAccess.Query.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Logistic;

public class GetLogisticRiderTagHandler : QueryBaseHandler, IRequestHandler<GetLogisticRiderTagQuery, QueryResponse<LogisticRiderTagResponse>>
{
    public GetLogisticRiderTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LogisticRiderTagResponse>> Handle(GetLogisticRiderTagQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.LogisticRiderTags
            .Include(x => x.LogisticRider)
            .Include(x => x.Tag)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (tag is null)
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
            Response = tag.Adapt<LogisticRiderTagResponse>()
        };
    }
}