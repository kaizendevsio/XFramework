using HealthEssentials.Core.DataAccess.Query.Entity.Schedule;
using HealthEssentials.Core.DataAccess.Query.Entity.Unit;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;
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
        var schedule = await _dataLayer.HealthEssentialsContext.Units
            .Include(x => x.Entity.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!schedule.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No units found",
                IsSuccess = true
            };
        }

        return new()
        {
            Message = "Units found",
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = schedule.Adapt<List<UnitResponse>>()
        };
    }
}