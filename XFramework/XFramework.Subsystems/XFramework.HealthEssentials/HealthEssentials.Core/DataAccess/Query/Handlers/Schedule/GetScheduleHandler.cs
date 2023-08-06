using HealthEssentials.Core.DataAccess.Query.Entity.Schedule;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Schedule;

public class GetScheduleHandler : QueryBaseHandler, IRequestHandler<GetScheduleQuery, QueryResponse<ScheduleResponse>>
{
    public GetScheduleHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ScheduleResponse>> Handle(GetScheduleQuery request, CancellationToken cancellationToken)
    {
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules
            .Include(x => x.Type)
            .Include(x => x.Priority)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (schedule is null)
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
            Response = schedule.Adapt<ScheduleResponse>()
        };
    }
}