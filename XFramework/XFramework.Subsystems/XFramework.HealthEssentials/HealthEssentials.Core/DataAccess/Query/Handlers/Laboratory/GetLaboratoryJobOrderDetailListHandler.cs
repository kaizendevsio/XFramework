using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderDetailListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderDetailListQuery, QueryResponse<List<LaboratoryJobOrderDetailResponse>>>
{
    public GetLaboratoryJobOrderDetailListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderDetailResponse>>> Handle(GetLaboratoryJobOrderDetailListQuery request, CancellationToken cancellationToken)
    {
        var detail = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderDetails
            .Include(x => x.LaboratoryService)
            .Include(x => x.LaboratoryJobOrder)
            .Where(x => EF.Functions.ILike(x.Remarks, $"%{request.SearchField}%"))
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!detail.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = detail.Adapt<List<LaboratoryJobOrderDetailResponse>>()
        };
    }
}