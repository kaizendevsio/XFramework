using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyTagListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyTagListQuery, QueryResponse<List<PharmacyTagResponse>>>
{
    public GetPharmacyTagListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyTagResponse>>> Handle(GetPharmacyTagListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}