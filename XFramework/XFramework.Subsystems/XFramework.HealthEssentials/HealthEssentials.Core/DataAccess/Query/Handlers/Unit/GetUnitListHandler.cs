using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Responses.Unit;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Unit;

public class GetUnitListHandler : QueryBaseHandler, IRequestHandler<GetUnitListQuery, QueryResponse<List<UnitResponse>>>
{
    public GetUnitListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<UnitResponse>>> Handle(GetUnitListQuery request, CancellationToken cancellationToken)
    {
        var unit = await _dataLayer.HealthEssentialsContext.Units
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!unit.Any())
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
            
            Response = unit.Adapt<List<UnitResponse>>()
        }; 
    }
}