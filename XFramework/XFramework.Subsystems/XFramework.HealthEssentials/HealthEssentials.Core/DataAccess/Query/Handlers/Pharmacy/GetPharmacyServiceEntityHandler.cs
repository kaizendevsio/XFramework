using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyServiceEntityHandler : QueryBaseHandler, IRequestHandler<GetPharmacyServiceEntityQuery, QueryResponse<PharmacyServiceEntityResponse>>
{
    public GetPharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyServiceEntityResponse>> Handle(GetPharmacyServiceEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}