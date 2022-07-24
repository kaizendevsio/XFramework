using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyJobOrderHandler : QueryBaseHandler, IRequestHandler<GetPharmacyJobOrderQuery, QueryResponse<PharmacyJobOrderResponse>>
{
    public GetPharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyJobOrderResponse>> Handle(GetPharmacyJobOrderQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}