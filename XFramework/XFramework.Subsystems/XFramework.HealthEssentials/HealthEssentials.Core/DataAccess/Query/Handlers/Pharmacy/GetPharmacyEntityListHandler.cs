using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyEntityListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyEntityListQuery, QueryResponse<List<PharmacyEntityResponse>>>
{
    public GetPharmacyEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyEntityResponse>>> Handle(GetPharmacyEntityListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}