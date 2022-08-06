using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryTagListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryTagListQuery, QueryResponse<List<LaboratoryTagResponse>>>
{
    public GetLaboratoryTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryTagResponse>>> Handle(GetLaboratoryTagListQuery request, CancellationToken cancellationToken)
    {
        var tag = await _dataLayer.HealthEssentialsContext.LaboratoryTags
            .Include(x => x.Tag)
            .Include(x => x.Laboratory)
            .OrderBy(x => x.CreatedAt)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);
            
        if (!tag.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            Response = tag.Adapt<List<LaboratoryTagResponse>>()
        };
    }
}