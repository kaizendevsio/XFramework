using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Unit;

public class GetUnitEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetUnitEntityGroupQuery, QueryResponse<UnitEntityGroupResponse>>
{
    public GetUnitEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<UnitEntityGroupResponse>> Handle(GetUnitEntityGroupQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.UnitEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (group is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No group found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Group found",
            Response = group.Adapt<UnitEntityGroupResponse>()
        };
    }
}