using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityGroupListQuery, QueryResponse<List<LaboratoryServiceEntityGroupResponse>>>
{
    public GetLaboratoryServiceEntityGroupListHandler()
    {
        
    }
    public async Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> Handle(GetLaboratoryServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryServiceEntityGroup = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .AsNoTracking()
            .Where(i => EF.Functions.Like(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .ToListAsync(CancellationToken.None);

        if (!laboratoryServiceEntityGroup.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Service Entity Group Found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Service Entity Group Found",
            IsSuccess = true,
            Response = laboratoryServiceEntityGroup.Adapt<List<LaboratoryServiceEntityGroupResponse>>()
        };
    }
}