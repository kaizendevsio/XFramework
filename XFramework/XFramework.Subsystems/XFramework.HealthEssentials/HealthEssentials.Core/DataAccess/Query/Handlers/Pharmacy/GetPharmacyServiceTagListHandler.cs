using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceTagListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceTagListQuery, QueryResponse<List<PharmacyServiceTagResponse>>>
{
    public GetPharmacyServiceTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyServiceTagResponse>>> Handle(GetPharmacyServiceTagListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}