using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyEntityHandler : QueryBaseHandler, IRequestHandler<GetPharmacyEntityQuery, QueryResponse<PharmacyEntityResponse>>
{
    public GetPharmacyEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyEntityResponse>> Handle(GetPharmacyEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}