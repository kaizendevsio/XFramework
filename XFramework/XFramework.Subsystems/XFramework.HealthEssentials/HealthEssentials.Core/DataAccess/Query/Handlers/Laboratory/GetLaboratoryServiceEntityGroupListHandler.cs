using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryServiceEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryServiceEntityGroupListQuery, QueryResponse<List<LaboratoryServiceEntityGroupResponse>>>
{
    public GetLaboratoryServiceEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryServiceEntityGroupResponse>>> Handle(GetLaboratoryServiceEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var laboratoryServiceEntityGroup = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntityGroups
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
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
            Response = laboratoryServiceEntityGroup.Adapt<List<LaboratoryServiceEntityGroupResponse>>()
        };
    }
}