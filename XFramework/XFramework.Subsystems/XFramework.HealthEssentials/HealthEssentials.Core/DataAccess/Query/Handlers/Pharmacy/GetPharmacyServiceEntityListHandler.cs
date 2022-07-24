using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceEntityListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceEntityListQuery, QueryResponse<List<PharmacyServiceEntityResponse>>>
{
    public GetPharmacyServiceEntityListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceEntityResponse>>> Handle(GetPharmacyServiceEntityListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}