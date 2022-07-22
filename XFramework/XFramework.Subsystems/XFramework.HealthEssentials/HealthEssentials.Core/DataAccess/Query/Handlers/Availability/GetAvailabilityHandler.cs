using HealthEssentials.Core.DataAccess.Query.Entity.Availability;
using HealthEssentials.Domain.Generics.Contracts.Responses.Availability;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Availability;

public class GetAvailabilityHandler : QueryBaseHandler, IRequestHandler<GetAvailabilityQuery, QueryResponse<AvailabilityResponse>>
{
    public GetAvailabilityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<AvailabilityResponse>> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}