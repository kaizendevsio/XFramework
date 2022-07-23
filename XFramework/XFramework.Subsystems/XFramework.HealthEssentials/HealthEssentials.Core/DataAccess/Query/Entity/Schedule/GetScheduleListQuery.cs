using HealthEssentials.Domain.Generics.Contracts.Requests.Schedule.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Schedule;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Schedule;

public class GetScheduleListQuery : GetScheduleListRequest, IRequest<QueryResponse<List<ScheduleResponse>>>
{
    
}