using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Unit;

public class GetUnitHandler : QueryBaseHandler, IRequestHandler<GetUnitQuery, QueryResponse<UnitResponse>>
{
    public GetUnitHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<UnitResponse>> Handle(GetUnitQuery request, CancellationToken cancellationToken)
    {
        var unit = await _dataLayer.HealthEssentialsContext.Units
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (unit is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No unit found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Unit found",
            Response = unit.Adapt<UnitResponse>()
        };
    }
}