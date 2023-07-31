using HealthEssentials.Core.DataAccess.Query.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Schedule;

public class GetScheduleListHandler : QueryBaseHandler, IRequestHandler<GetScheduleListQuery, QueryResponse<List<ScheduleResponse>>>
{
    public GetScheduleListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<ScheduleResponse>>> Handle(GetScheduleListQuery request, CancellationToken cancellationToken)
    {
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules
            .Include(x => x.Entity)
            .Include(x => x.Priority)
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
                Message = "No schedule found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Schedule found",
            
            Response = schedule.Adapt<List<ScheduleResponse>>()
        };
    }
}