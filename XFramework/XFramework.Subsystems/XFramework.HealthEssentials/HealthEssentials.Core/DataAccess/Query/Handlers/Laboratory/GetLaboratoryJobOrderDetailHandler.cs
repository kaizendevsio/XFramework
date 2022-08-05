using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderDetailHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderDetailQuery, QueryResponse<LaboratoryJobOrderDetailResponse>>
{
    public GetLaboratoryJobOrderDetailHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<LaboratoryJobOrderDetailResponse>> Handle(GetLaboratoryJobOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var detail = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderDetails
            .Include(x => x.LaboratoryService)
            .Include(x => x.LaboratoryJobOrder)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (detail is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Data Found",
            Response = detail.Adapt<LaboratoryJobOrderDetailResponse>()
        };
    }
}