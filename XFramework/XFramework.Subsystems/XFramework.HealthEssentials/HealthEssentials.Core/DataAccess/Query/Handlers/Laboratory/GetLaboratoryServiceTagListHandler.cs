using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceTagListQuery, QueryResponse<List<LaboratoryServiceTagResponse>>>
{
    public GetLaboratoryServiceTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryServiceTagResponse>>> Handle(GetLaboratoryServiceTagListQuery request, CancellationToken cancellationToken)
    {
        var serviceTag = await _dataLayer.HealthEssentialsContext.LaboratoryServiceTags
            .Include(x => x.LaboratoryService)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);
        
        if (!serviceTag.Any())
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
            Response = serviceTag.Adapt<List<LaboratoryServiceTagResponse>>()
        };
    }
}