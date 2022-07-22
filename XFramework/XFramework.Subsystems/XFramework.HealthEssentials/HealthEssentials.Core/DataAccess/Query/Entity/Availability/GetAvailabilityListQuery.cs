using HealthEssentials.Domain.Generics.Contracts.Requests.Availability;
using HealthEssentials.Domain.Generics.Contracts.Responses.Availability;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Availability;

public class GetAvailabilityListQuery : GetAvailabilityListRequest, IRequest<QueryResponse<List<AvailabilityResponse>>>
{
    
}