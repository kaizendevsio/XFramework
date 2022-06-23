using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceListQuery, QueryResponse<List<LaboratoryServiceEntityResponse>>>
{
    public GetLaboratoryServiceListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> Handle(GetLaboratoryServiceListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryServiceEntities = await _dataLayer.HealthEssentialsContext.LaboratoryServices
            .Include(i => i.Entity)
            .ThenInclude(i => i.Group)
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!laboratoryServiceEntities.Any())
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
            Message = "Success",
            IsSuccess = true,
            Response = laboratoryServiceEntities.Adapt<List<LaboratoryServiceEntityResponse>>()
        };
        
    }
}