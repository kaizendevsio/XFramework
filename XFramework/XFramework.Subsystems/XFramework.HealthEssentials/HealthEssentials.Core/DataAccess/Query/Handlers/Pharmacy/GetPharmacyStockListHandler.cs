using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyStockListHandler : QueryBaseHandler, IRequestHandler<GetPharmacyStockListQuery, QueryResponse<List<PharmacyStockResponse>>>
{
    public GetPharmacyStockListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<PharmacyStockResponse>>> Handle(GetPharmacyStockListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}