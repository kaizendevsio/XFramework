using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultQuery, QueryResponse<LaboratoryJobOrderResultResponse>>
{
    public GetLaboratoryJobOrderResultHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryJobOrderResultResponse>> Handle(GetLaboratoryJobOrderResultQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults
            .Include(x => x.LaboratoryJobOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (result is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No result found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Result found",
            Response = result.Adapt<LaboratoryJobOrderResultResponse>()
        };
    }
}