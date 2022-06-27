using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyLocationListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyLocationListQuery, QueryResponse<List<PharmacyLocationResponse>>>
{
    public GetPharmacyLocationListHandler()
    {
        
    }
    public async Task<QueryResponse<List<PharmacyLocationResponse>>> Handle(GetPharmacyLocationListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}