using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceListQuery, QueryResponse<List<PharmacyServiceResponse>>>
{
    public GetPharmacyServiceListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceResponse>>> Handle(GetPharmacyServiceListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}