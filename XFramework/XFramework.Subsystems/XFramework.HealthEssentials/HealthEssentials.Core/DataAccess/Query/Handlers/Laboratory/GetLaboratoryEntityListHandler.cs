using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityListQuery, QueryResponse<List<LaboratoryEntityResponse>>>
{
    public GetLaboratoryEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryEntityResponse>>> Handle(GetLaboratoryEntityListQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.LaboratoryEntities
            .Include(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
            
        if (!entity.Any())
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
            Response = entity.Adapt<List<LaboratoryEntityResponse>>()
        };
    }
}