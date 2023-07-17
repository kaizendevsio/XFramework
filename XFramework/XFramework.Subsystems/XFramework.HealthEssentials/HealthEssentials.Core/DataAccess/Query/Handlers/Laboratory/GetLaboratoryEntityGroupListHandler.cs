using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryEntityGroupListQuery, QueryResponse<List<LaboratoryEntityGroupResponse>>>
{
    public GetLaboratoryEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryEntityGroupResponse>>> Handle(GetLaboratoryEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.LaboratoryEntityGroups
            .Where(x => EF.Functions.ILike(x.Name, $"{request.SearchField}"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!group.Any())
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
            Response = group.Adapt<List<LaboratoryEntityGroupResponse>>()
        };
    }
}