using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyStockHandler : QueryBaseHandler, IRequestHandler<GetPharmacyStockQuery, QueryResponse<PharmacyStockResponse>>
{
    public GetPharmacyStockHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyStockResponse>> Handle(GetPharmacyStockQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}