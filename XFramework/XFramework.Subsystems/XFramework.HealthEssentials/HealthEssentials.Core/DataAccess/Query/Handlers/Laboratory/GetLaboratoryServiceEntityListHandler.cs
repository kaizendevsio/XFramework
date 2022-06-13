using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityListQuery, QueryResponse<List<LaboratoryServiceEntityResponse>>>
{
    public GetLaboratoryServiceEntityListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryServiceEntityResponse>>> Handle(GetLaboratoryServiceEntityListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryServiceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        if (!laboratoryServiceEntity.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Service Entity Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Service Entity Found",
            IsSuccess = true,
            Response = laboratoryServiceEntity.Adapt<List<LaboratoryServiceEntityResponse>>()
        };
    }
}