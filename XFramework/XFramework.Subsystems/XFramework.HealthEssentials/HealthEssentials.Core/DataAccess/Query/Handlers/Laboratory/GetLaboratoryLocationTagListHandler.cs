using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationTagListQuery, QueryResponse<List<LaboratoryLocationTagResponse>>>
{
    public GetLaboratoryLocationTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryLocationTagResponse>>> Handle(GetLaboratoryLocationTagListQuery request, CancellationToken cancellationToken)
    {
        var locationTag = await _dataLayer.HealthEssentialsContext.LaboratoryLocationTags
            .Include(x => x.LaboratoryLocation)
            .ThenInclude(x => x.Laboratory)
            .Include(x => x.Tag)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);
        
        if (!locationTag.Any())
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
            Response = locationTag.Adapt<List<LaboratoryLocationTagResponse>>()
        };
    }
}