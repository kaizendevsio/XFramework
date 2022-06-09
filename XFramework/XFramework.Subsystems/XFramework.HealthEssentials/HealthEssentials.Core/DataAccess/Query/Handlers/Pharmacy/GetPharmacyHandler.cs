using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyHandler : QueryBaseHandler, IRequestHandler<GetPharmacyQuery, QueryResponse<PharmacyResponse>>
{
    public GetPharmacyHandler()
    {
        
    }
    public async Task<QueryResponse<PharmacyResponse>> Handle(GetPharmacyQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}