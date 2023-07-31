using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationListQuery, QueryResponse<List<LaboratoryLocationResponse>>>
{
    public GetLaboratoryLocationListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LaboratoryLocationResponse>>> Handle(GetLaboratoryLocationListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations
            .Include(i => i.Laboratory)
            .Include(i => i.LaboratoryServices)
            .Include(i => i.LaboratoryMembers)
            .Include(i => i.LaboratoryLocationTags)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryLocation.Any())
        {
            return new ()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No data found",
                
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory location found",
            
            Response = laboratoryLocation.Adapt<List<LaboratoryLocationResponse>>()
        };
    }
}