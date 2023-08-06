using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceListQuery, QueryResponse<List<LaboratoryServiceResponse>>>
{
    public GetLaboratoryServiceListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LaboratoryServiceResponse>>> Handle(GetLaboratoryServiceListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServices
            .Include(x => x.Type)
            .ThenInclude(x => x.Group)
            .Include(x => x.LaboratoryLocation)
            .ThenInclude(x => x.Laboratory)
            .Include(x => x.Unit)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryService.Any())
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
            Response = laboratoryService.Adapt<List<LaboratoryServiceResponse>>()
        };
        
    }
}