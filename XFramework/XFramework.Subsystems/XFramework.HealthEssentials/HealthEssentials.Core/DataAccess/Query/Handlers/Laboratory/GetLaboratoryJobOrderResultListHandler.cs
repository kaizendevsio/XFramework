using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryJobOrderResultListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryJobOrderResultListQuery, QueryResponse<List<LaboratoryJobOrderResultResponse>>>
{
    public GetLaboratoryJobOrderResultListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryJobOrderResultResponse>>> Handle(GetLaboratoryJobOrderResultListQuery request, CancellationToken cancellationToken)
    {
        var result = await _dataLayer.HealthEssentialsContext.LaboratoryJobOrderResults
            .Include(x => x.LaboratoryJobOrder)
            .Where(x => EF.Functions.ILike(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!result.Any())
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
            Response = result.Adapt<List<LaboratoryJobOrderResultResponse>>()
        };
    }
}