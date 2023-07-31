using HealthEssentials.Core.DataAccess.Query.Entity.Ailment;
using HealthEssentials.Domain.Generics.Contracts.Responses.Ailment;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Ailment;

public class GetAilmentEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetAilmentEntityGroupListQuery, QueryResponse<List<AilmentEntityGroupResponse>>>
{
    public GetAilmentEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<AilmentEntityGroupResponse>>> Handle(GetAilmentEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var ailmentEntityGroup = await _dataLayer.HealthEssentialsContext.AilmentEntityGroups
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);

        if (!ailmentEntityGroup.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Ailment Entity Group Found",
                
            }; 
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Ailment Entity Group Found",
            
            Response = ailmentEntityGroup.Adapt<List<AilmentEntityGroupResponse>>()
        };
    }
}